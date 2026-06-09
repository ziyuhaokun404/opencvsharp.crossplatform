using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics;
using OpenCvSharp;

namespace OpenCvSharp.Core;

public interface ITemplateLocator
{
    string Name { get; }

    TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options);
}

public sealed class MatchTemplateLocator : ITemplateLocator
{
    /// <summary>
    /// Maximum result matrix area (cols × rows) before pyramid downsampling kicks in.
    /// At 50k pixels, MatchTemplate runs in ~1-3ms on typical hardware.
    /// </summary>
    private const int PyramidThresholdPixels = 50_000;

    /// <summary>
    /// Minimum template dimension after downsampling. Below this, matching quality degrades.
    /// </summary>
    private const int MinTemplateEdge = 12;
    private const int InitialMatchCapacityDivisor = 64;
    private const int MinInitialMatchCapacity = 256;
    private const int MaxInitialMatchCapacity = 32768;

    public string Name => "模板匹配";

    public TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options)
    {
        var profile = new PerformanceProfile($"模板匹配 ({source.Width}×{source.Height}, 模板 {template.Width}×{template.Height})");
        var stopwatch = Stopwatch.StartNew();

        // 仅灰度转换时分配新 Mat；彩色模式直接使用原 Mat，不 Clone
        using var graySource = options.UseGrayscale ? ConvertToGray(source) : null;
        using var grayTemplate = options.UseGrayscale ? ConvertToGray(template) : null;
        var matchSource = graySource ?? source;
        var matchTemplate = grayTemplate ?? template;
        profile.Step("PrepareForMatching", options.UseGrayscale ? "灰度" : "直接（无 Clone）");

        var resultCols = matchSource.Width - matchTemplate.Width + 1;
        var resultRows = matchSource.Height - matchTemplate.Height + 1;
        var resultArea = resultCols * resultRows;
        var scale = ComputePyramidScale(resultArea, matchTemplate.Width, matchTemplate.Height);

        Mat workSource, workTemplate;
        bool usedPyramid = scale < 1.0;

        if (usedPyramid)
        {
            workSource = new Mat();
            Cv2.Resize(matchSource, workSource, new Size(0, 0), scale, scale, InterpolationFlags.Linear);
            workTemplate = new Mat();
            Cv2.Resize(matchTemplate, workTemplate, new Size(0, 0), scale, scale, InterpolationFlags.Linear);
            profile.Step("Pyramid.Resize", $"×{scale:F2} → {workSource.Width}×{workSource.Height}, 模板 {workTemplate.Width}×{workTemplate.Height}");
        }
        else
        {
            workSource = matchSource;
            workTemplate = matchTemplate;
        }

        try
        {
            using var result = new Mat();
            Cv2.MatchTemplate(workSource, workTemplate, result, options.Method);
            profile.Step("MatchTemplate", $"{result.Cols}×{result.Rows}" + (usedPyramid ? " (缩放)" : ""));

            Cv2.MinMaxLoc(result, out var minValue, out var maxValue, out var minLocation, out var maxLocation);
            profile.Step("MinMaxLoc");

            var bestLocRaw = options.HigherIsBetter ? maxLocation : minLocation;
            var bestScore = options.HigherIsBetter ? maxValue : 1.0 - minValue;

            // Map best location back to original coordinates
            var bestLocation = usedPyramid
                ? new Point((int)Math.Round(bestLocRaw.X / scale), (int)Math.Round(bestLocRaw.Y / scale))
                : bestLocRaw;

            // Find all candidates, mapping coordinates if pyramid was used
            var candidates = FindMatchesUnsafe(result, template.Size(), options, scale: usedPyramid ? scale : 1.0);
            profile.Step("FindMatches(unsafe)", $"候选 {candidates.Count}");

            var matches = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, options.NmsOverlapThreshold);
            profile.Step("NMS", $"匹配 {matches.Count}");

            stopwatch.Stop();
            var profileResult = profile.Finish();

            return new TemplateLocatorResult(
                bestLocation,
                bestScore,
                template.Size(),
                candidates,
                matches,
                stopwatch.Elapsed,
                profileResult,
                scale,
                matchSource.Size(),
                matchTemplate.Size(),
                workSource.Size(),
                workTemplate.Size(),
                result.Size(),
                PyramidThresholdPixels,
                MinTemplateEdge);
        }
        finally
        {
            if (usedPyramid)
            {
                workSource.Dispose();
                workTemplate.Dispose();
            }
        }
    }

    /// <summary>
    /// Computes the downsampling scale factor. Returns 1.0 if no pyramid is needed.
    /// </summary>
    private static double ComputePyramidScale(int resultArea, int templateWidth, int templateHeight)
    {
        if (resultArea <= PyramidThresholdPixels)
            return 1.0;

        var scale = Math.Sqrt((double)PyramidThresholdPixels / resultArea);
        // Ensure template doesn't get too small after downsampling
        var minDim = Math.Min(templateWidth, templateHeight);
        var scaledMinDim = minDim * scale;
        if (scaledMinDim < MinTemplateEdge)
            scale = (double)MinTemplateEdge / minDim;

        return Math.Min(scale, 1.0);
    }

    private static Mat ConvertToGray(Mat source)
    {
        var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    /// <summary>
    /// Extracts candidates from a result matrix using unsafe pointer access.
    /// When scale &lt; 1.0, coordinates are mapped back to original image space.
    /// </summary>
    private static unsafe List<MatchCandidate> FindMatchesUnsafe(
        Mat result, Size templateSize, TemplateLocatorOptions options, double scale = 1.0)
    {
        var rows = result.Rows;
        var cols = result.Cols;
        var step = (int)(result.Step() / sizeof(float));
        var ptr = (float*)result.DataPointer;
        var higherIsBetter = options.HigherIsBetter;
        var fThreshold = (float)options.Threshold;
        var tw = templateSize.Width;
        var th = templateSize.Height;
        var needsRemap = scale < 1.0;
        var invScale = needsRemap ? 1.0 / scale : 1.0;

        var matches = new List<MatchCandidate>(EstimateInitialMatchCapacity(rows, cols));

        var mappedX = new int[cols];
        if (needsRemap)
        {
            for (var x = 0; x < cols; x++) mappedX[x] = (int)Math.Round(x * invScale);
        }
        else
        {
            for (var x = 0; x < cols; x++) mappedX[x] = x;
        }

        var vecLength = Vector256<float>.Count;
        var loopLimit = cols - (cols % vecLength);
        var vec128Length = Vector128<float>.Count;
        var loopLimit128 = cols - (cols % vec128Length);

        if (higherIsBetter)
        {
            var thresholdVec = Vector256.Create(fThreshold);
            var thresholdVec128 = Vector128.Create(fThreshold);
            for (var y = 0; y < rows; y++)
            {
                var row = ptr + y * step;
                var my = needsRemap ? (int)Math.Round(y * invScale) : y;
                
                var x = 0;
                if (Vector256.IsHardwareAccelerated)
                {
                    for (; x < loopLimit; x += vecLength)
                    {
                        var v = Vector256.Load(row + x);
                        var mask = Vector256.GreaterThanOrEqual(v, thresholdVec);
                        var bits = mask.ExtractMostSignificantBits();
                        
                        if (bits != 0)
                        {
                            for (var i = 0; i < vecLength; i++)
                            {
                                var cx = x + i;
                                var raw = row[cx];
                                if (raw >= fThreshold)
                                {
                                    matches.Add(new MatchCandidate(new Rect(mappedX[cx], my, tw, th), (double)raw));
                                }
                            }
                        }
                    }
                }
                else if (Vector128.IsHardwareAccelerated)
                {
                    for (; x < loopLimit128; x += vec128Length)
                    {
                        var v = Vector128.Load(row + x);
                        var mask = Vector128.GreaterThanOrEqual(v, thresholdVec128);
                        var bits = mask.ExtractMostSignificantBits();
                        
                        if (bits != 0)
                        {
                            for (var i = 0; i < vec128Length; i++)
                            {
                                var cx = x + i;
                                var raw = row[cx];
                                if (raw >= fThreshold)
                                {
                                    matches.Add(new MatchCandidate(new Rect(mappedX[cx], my, tw, th), (double)raw));
                                }
                            }
                        }
                    }
                }
                
                for (; x < cols; x++)
                {
                    var raw = row[x];
                    if (raw >= fThreshold)
                    {
                        matches.Add(new MatchCandidate(new Rect(mappedX[x], my, tw, th), (double)raw));
                    }
                }
            }
        }
        else
        {
            var reversedThreshold = 1.0f - fThreshold;
            var reversedThresholdVec = Vector256.Create(reversedThreshold);
            var reversedThresholdVec128 = Vector128.Create(reversedThreshold);
            for (var y = 0; y < rows; y++)
            {
                var row = ptr + y * step;
                var my = needsRemap ? (int)Math.Round(y * invScale) : y;

                var x = 0;
                if (Vector256.IsHardwareAccelerated)
                {
                    for (; x < loopLimit; x += vecLength)
                    {
                        var v = Vector256.Load(row + x);
                        var mask = Vector256.LessThanOrEqual(v, reversedThresholdVec);
                        var bits = mask.ExtractMostSignificantBits();
                        
                        if (bits != 0)
                        {
                            for (var i = 0; i < vecLength; i++)
                            {
                                var cx = x + i;
                                var raw = row[cx];
                                var score = 1.0f - raw;
                                if (score >= fThreshold)
                                {
                                    matches.Add(new MatchCandidate(new Rect(mappedX[cx], my, tw, th), (double)score));
                                }
                            }
                        }
                    }
                }
                else if (Vector128.IsHardwareAccelerated)
                {
                    for (; x < loopLimit128; x += vec128Length)
                    {
                        var v = Vector128.Load(row + x);
                        var mask = Vector128.LessThanOrEqual(v, reversedThresholdVec128);
                        var bits = mask.ExtractMostSignificantBits();
                        
                        if (bits != 0)
                        {
                            for (var i = 0; i < vec128Length; i++)
                            {
                                var cx = x + i;
                                var raw = row[cx];
                                var score = 1.0f - raw;
                                if (score >= fThreshold)
                                {
                                    matches.Add(new MatchCandidate(new Rect(mappedX[cx], my, tw, th), (double)score));
                                }
                            }
                        }
                    }
                }

                for (; x < cols; x++)
                {
                    var raw = row[x];
                    var score = 1.0f - raw;
                    if (score >= fThreshold)
                    {
                        matches.Add(new MatchCandidate(new Rect(mappedX[x], my, tw, th), (double)score));
                    }
                }
            }
        }

        return matches;
    }

    private static int EstimateInitialMatchCapacity(int rows, int cols)
    {
        var area = (long)rows * cols;
        if (area <= 0)
            return 0;

        // 限制预估分配；高阈值场景下实际候选通常远少于结果像素数。
        var upperBound = (int)Math.Min(area, MaxInitialMatchCapacity);
        var lowerBound = Math.Min(MinInitialMatchCapacity, upperBound);
        var estimate = (int)Math.Min(area / InitialMatchCapacityDivisor, int.MaxValue);
        return Math.Clamp(estimate, lowerBound, upperBound);
    }

}

public sealed class ContourTemplateLocator : ITemplateLocator
{
    private const int ContourPyramidThresholdPixels = 500_000;
    private const int MinContourTemplateEdge = 64;
    private const double DuplicateBoundsOverlapThreshold = 0.86;
    private static readonly double[] TrainingCoverageCandidates = [0.35, 0.45, 0.50];
    private static readonly double[] TrainingDistanceToleranceScales = [0.85, 1.15];
    private static readonly double[] TrainingThresholdCandidates = [0.35, 0.45, 0.55, 0.65];
    private static readonly Scalar PreviewContourColor = new(235, 99, 37);
    private static readonly ContourExtractionSettings DefaultSettings = new(20, 24, 72, 1.0, 1, 16, 0.5, 8);
    private ContourExtractionSettings currentSettings = DefaultSettings;
    private ContourPyramidCache? pyramidCache;

    public string Name => "轮廓匹配";

    public ContourExtractionSettings CurrentSettings => currentSettings;

    public void ApplySettings(ContourExtractionSettings settings)
    {
        currentSettings = settings;
    }

    public TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options)
    {
        return Locate(source, template, options, currentSettings, this);
    }

    public ContourTrainingResult Train(Mat source, Mat template, double nmsOverlapThreshold)
    {
        var stopwatch = Stopwatch.StartNew();
        var best = ContourTrainingEvaluation.Empty;

        var resultCols = source.Width - template.Width + 1;
        var resultRows = source.Height - template.Height + 1;
        var resultArea = (long)resultCols * resultRows;
        var scale = ComputeContourPyramidScale(resultArea, template.Width, template.Height);
        var usedPyramid = scale < 1.0;
        Mat workSource = source;
        Mat workTemplate = template;

        if (usedPyramid)
        {
            workSource = CreateResizedGray(source, scale);
            workTemplate = CreateResizedGray(template, scale);
        }

        try
        {
            foreach (var baseSettings in CreateTrainingCandidates(template.Size()))
            {
                var workBaseSettings = usedPyramid ? ScaleContourSettings(baseSettings, scale) : baseSettings;
                using var response = BuildContourResponse(workSource, workTemplate, workBaseSettings, null);
                if (response.EdgeCount == 0)
                    continue;

                foreach (var settings in CreateTrainingExtractionCandidates(baseSettings))
                {
                    var workSettings = usedPyramid ? ScaleContourSettings(settings, scale) : settings;
                    foreach (var threshold in TrainingThresholdCandidates)
                    {
                        var evaluation = EvaluateTrainingCandidate(
                            response,
                            settings,
                            workSettings,
                            threshold,
                            nmsOverlapThreshold,
                            usedPyramid ? scale : 1.0,
                            source.Size(),
                            template.Size());
                        if (evaluation.TrainingScore > best.TrainingScore)
                            best = evaluation;
                    }
                }
            }
        }
        finally
        {
            if (usedPyramid)
            {
                workSource.Dispose();
                workTemplate.Dispose();
            }
        }

        if (best.Settings is null)
            throw new InvalidOperationException("无法从当前图像训练轮廓模板。");

        currentSettings = best.Settings;
        stopwatch.Stop();
        return new ContourTrainingResult(
            best.Settings,
            best.TemplateContourCount,
            best.CandidateCount,
            best.MatchCount,
            best.BestScore,
            best.SuggestedThreshold,
            stopwatch.Elapsed);
    }

    public Mat CreateTemplateContourPreview(Mat template)
    {
        using var templateEdges = BuildEdges(template, currentSettings);
        var templateProfile = CreateTemplateProfile(template.Size(), templateEdges, currentSettings);
        var preview = template.Clone();
        Cv2.DrawContours(
            preview,
            templateProfile.Contours.Select(contour => contour.Contour).ToArray(),
            -1,
            PreviewContourColor,
            3,
            LineTypes.AntiAlias);
        return preview;
    }

    private static TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options, ContourExtractionSettings settings, ContourTemplateLocator locator)
    {
        var profile = new PerformanceProfile($"轮廓匹配 ({source.Width}×{source.Height}, 模板 {template.Width}×{template.Height})");
        var stopwatch = Stopwatch.StartNew();

        var resultCols = source.Width - template.Width + 1;
        var resultRows = source.Height - template.Height + 1;
        var resultArea = (long)resultCols * resultRows;
        var scale = ComputeContourPyramidScale(resultArea, template.Width, template.Height);
        var usedPyramid = scale < 1.0;
        var workSettings = usedPyramid ? ScaleContourSettings(settings, scale) : settings;

        Mat workSource = source;
        Mat workTemplate = template;
        ContourPyramidCache? activePyramidCache = null;

        if (usedPyramid)
        {
            var cache = locator.GetOrCreatePyramidCache(source, template, scale, out var cacheHit);
            activePyramidCache = cache;
            workSource = cache.WorkSource;
            workTemplate = cache.WorkTemplate;
            profile.Step("Pyramid.Resize", $"×{scale:F3} → {workSource.Width}×{workSource.Height}, 模板 {workTemplate.Width}×{workTemplate.Height}" + (cacheHit ? " (缓存)" : ""));
        }

        using var uncachedResponse = usedPyramid
            ? null
            : BuildContourResponse(workSource, workTemplate, workSettings, profile);
        var response = usedPyramid
            ? locator.GetOrCreateResponseCache(activePyramidCache!, workSettings, profile)
            : uncachedResponse!;

        var candidates = ExtractEdgeResponseCandidatesOptimized(
            response.DistanceSum,
            response.HitSum,
            response.EdgeCount,
            response.TemplateSize,
            options.Threshold,
            workSettings.MinimumTemplateContourCoverage,
            workSettings.EdgeDistanceTolerance,
            profile);
        candidates = RescoreCandidatesByIntensity(response.IntensityResult, candidates, options.Threshold);
        profile.Step("IntensityVerify", $"候选 {candidates.Count}");
        if (usedPyramid)
            candidates = MapCandidatesToOriginal(candidates, scale, source.Size(), template.Size());
        profile.Step("FindEdgeResponseMatches(汇总)", $"候选 {candidates.Count}");

        var matches = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, options.NmsOverlapThreshold);
        profile.Step("NMS", $"匹配 {matches.Count}");

        stopwatch.Stop();
        var profileResult = profile.Finish();

        var bestLocation = new Point(0, 0);
        var bestScore = 0.0;
        if (candidates.Count > 0)
        {
            bestLocation = candidates[0].Rect.Location;
            bestScore = candidates[0].Score;
            for (var i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Score > bestScore)
                {
                    bestScore = candidates[i].Score;
                    bestLocation = candidates[i].Rect.Location;
                }
            }
        }
        return new TemplateLocatorResult(
            bestLocation,
            bestScore,
            template.Size(),
            candidates,
            matches,
            stopwatch.Elapsed,
            profileResult,
            scale,
            source.Size(),
            template.Size(),
            workSource.Size(),
            workTemplate.Size(),
            new Size(workSource.Width - workTemplate.Width + 1, workSource.Height - workTemplate.Height + 1),
            ContourPyramidThresholdPixels,
            MinContourTemplateEdge);
    }

    private ContourPyramidCache GetOrCreatePyramidCache(Mat source, Mat template, double scale, out bool cacheHit)
    {
        var existing = pyramidCache;
        if (existing is not null && existing.Matches(source, template, scale))
        {
            cacheHit = true;
            return existing;
        }

        existing?.Dispose();
        var workSource = CreateResizedGray(source, scale);
        var workTemplate = CreateResizedGray(template, scale);
        pyramidCache = new ContourPyramidCache(source, template, scale, workSource, workTemplate);
        cacheHit = false;
        return pyramidCache;
    }

    private ContourResponseCache GetOrCreateResponseCache(
        ContourPyramidCache pyramid,
        ContourExtractionSettings settings,
        PerformanceProfile profile)
    {
        var existing = pyramid.ResponseCache;
        if (existing is not null && existing.Matches(settings))
        {
            profile.Step("ContourResponse", $"缓存，轮廓 {existing.TemplateContourCount}");
            return existing;
        }

        existing?.Dispose();
        var response = BuildContourResponse(pyramid.WorkSource, pyramid.WorkTemplate, settings, profile);
        pyramid.ResponseCache = response;
        return response;
    }

    private static Mat CreateResizedGray(Mat image, double scale)
    {
        using var gray = new Mat();
        if (image.Channels() == 1)
            image.CopyTo(gray);
        else
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

        var resized = new Mat();
        Cv2.Resize(gray, resized, new Size(0, 0), scale, scale, InterpolationFlags.Area);
        return resized;
    }

    private static ContourResponseCache BuildContourResponse(
        Mat workSource,
        Mat workTemplate,
        ContourExtractionSettings settings,
        PerformanceProfile? profile)
    {
        using var sourceEdges = BuildEdges(workSource, settings);
        profile?.Step("BuildEdges(源图)");

        using var templateEdges = BuildEdges(workTemplate, settings);
        profile?.Step("BuildEdges(模板)");

        var templateProfile = CreateTemplateProfile(workTemplate.Size(), templateEdges, settings);
        profile?.Step("CreateTemplateProfile", $"轮廓 {templateProfile.Contours.Count}");

        using var templateProfileEdges = CreateTemplateProfileEdges(workTemplate.Size(), templateProfile);
        profile?.Step("CreateTemplateProfileEdges");

        var edgeCount = Cv2.CountNonZero(templateProfileEdges);
        if (edgeCount == 0)
            return ContourResponseCache.Empty(settings, workTemplate.Size(), templateProfile.Contours.Count);

        using var invertedSourceEdges = new Mat();
        Cv2.BitwiseNot(sourceEdges, invertedSourceEdges);
        using var distance = new Mat();
        Cv2.DistanceTransform(invertedSourceEdges, distance, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        profile?.Step("DistanceTransform");

        using var templateMask = new Mat();
        templateProfileEdges.ConvertTo(templateMask, MatType.CV_32F, 1.0 / 255.0);

        var distanceSum = new Mat();
        Cv2.MatchTemplate(distance, templateMask, distanceSum, TemplateMatchModes.CCorr);
        profile?.Step("MatchTemplate(距离)");

        using var sourceEdgeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var expandedSourceEdges = new Mat();
        Cv2.Dilate(sourceEdges, expandedSourceEdges, sourceEdgeKernel);
        using var sourceMask = new Mat();
        expandedSourceEdges.ConvertTo(sourceMask, MatType.CV_32F, 1.0 / 255.0);

        var hitSum = new Mat();
        Cv2.MatchTemplate(sourceMask, templateMask, hitSum, TemplateMatchModes.CCorr);
        profile?.Step("MatchTemplate(命中)");

        var intensityResult = new Mat();
        Cv2.MatchTemplate(workSource, workTemplate, intensityResult, TemplateMatchModes.CCoeffNormed);
        profile?.Step("MatchTemplate(灰度)");

        return new ContourResponseCache(settings, workTemplate.Size(), templateProfile.Contours.Count, edgeCount, distanceSum, hitSum, intensityResult);
    }

    private static double ComputeContourPyramidScale(long resultArea, int templateWidth, int templateHeight)
    {
        if (resultArea <= ContourPyramidThresholdPixels)
            return 1.0;

        var scale = Math.Sqrt((double)ContourPyramidThresholdPixels / resultArea);
        var minDim = Math.Min(templateWidth, templateHeight);
        var scaledMinDim = minDim * scale;
        if (scaledMinDim < MinContourTemplateEdge)
            scale = (double)MinContourTemplateEdge / minDim;

        return Math.Min(scale, 1.0);
    }

    private static ContourExtractionSettings ScaleContourSettings(ContourExtractionSettings settings, double scale)
    {
        return new ContourExtractionSettings(
            Math.Max(1, settings.MinimumContourArea * scale * scale),
            settings.CannyLowThreshold,
            settings.CannyHighThreshold,
            settings.GradientThresholdScale,
            settings.CloseIterations,
            settings.MaximumTemplateContours,
            settings.MinimumTemplateContourCoverage,
            Math.Max(1, settings.EdgeDistanceTolerance * scale));
    }

    private static List<MatchCandidate> MapCandidatesToOriginal(
        IReadOnlyList<MatchCandidate> candidates,
        double scale,
        Size sourceSize,
        Size templateSize)
    {
        var mapped = new List<MatchCandidate>(candidates.Count);
        var maxX = Math.Max(0, sourceSize.Width - templateSize.Width);
        var maxY = Math.Max(0, sourceSize.Height - templateSize.Height);

        foreach (var candidate in candidates)
        {
            var x = Math.Clamp((int)Math.Round(candidate.Rect.X / scale), 0, maxX);
            var y = Math.Clamp((int)Math.Round(candidate.Rect.Y / scale), 0, maxY);
            mapped.Add(new MatchCandidate(new Rect(x, y, templateSize.Width, templateSize.Height), candidate.Score));
        }

        // 用 Dictionary 去重：保留每个 Rect 对应的最高分候选
        var deduped = new Dictionary<Rect, MatchCandidate>(mapped.Count);
        foreach (var candidate in mapped)
        {
            if (!deduped.TryGetValue(candidate.Rect, out var existing) || candidate.Score > existing.Score)
                deduped[candidate.Rect] = candidate;
        }
        var result = new List<MatchCandidate>(deduped.Count);
        result.AddRange(deduped.Values);
        result.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        return result;
    }

    private static IEnumerable<ContourExtractionSettings> CreateTrainingCandidates(Size templateSize)
    {
        var templateArea = templateSize.Width * templateSize.Height;
        var areaCandidates = new[]
        {
            Math.Max(8, templateArea * 0.00004),
            Math.Max(12, templateArea * 0.00008),
            Math.Max(20, templateArea * 0.00016)
        };

        var distanceTolerance = Math.Max(4, Math.Min(templateSize.Width, templateSize.Height) * 0.02);
        foreach (var minArea in areaCandidates)
        {
            foreach (var cannyLow in new[] { 16, 32 })
            {
                foreach (var gradientScale in new[] { 0.8, 1.1 })
                {
                    foreach (var closeIterations in new[] { 0, 1 })
                    {
                        yield return new ContourExtractionSettings(
                            minArea,
                            cannyLow,
                            cannyLow * 3,
                            gradientScale,
                            closeIterations,
                            24,
                            0.45,
                            distanceTolerance);
                    }
                }
            }
        }
    }

    private static IEnumerable<ContourExtractionSettings> CreateTrainingExtractionCandidates(ContourExtractionSettings baseSettings)
    {
        foreach (var coverage in TrainingCoverageCandidates)
        {
            foreach (var distanceScale in TrainingDistanceToleranceScales)
            {
                yield return baseSettings with
                {
                    MinimumTemplateContourCoverage = coverage,
                    EdgeDistanceTolerance = Math.Max(1, baseSettings.EdgeDistanceTolerance * distanceScale)
                };
            }
        }
    }

    private static ContourTrainingEvaluation EvaluateTrainingCandidate(
        ContourResponseCache response,
        ContourExtractionSettings settings,
        ContourExtractionSettings workSettings,
        double threshold,
        double nmsOverlapThreshold,
        double scale,
        Size sourceSize,
        Size templateSize)
    {
        var edgeCandidates = ExtractEdgeResponseCandidatesOptimized(
            response.DistanceSum,
            response.HitSum,
            response.EdgeCount,
            response.TemplateSize,
            threshold,
            workSettings.MinimumTemplateContourCoverage,
            workSettings.EdgeDistanceTolerance);
        if (edgeCandidates.Count == 0)
            return ContourTrainingEvaluation.Empty;

        var candidates = RescoreCandidatesByIntensity(response.IntensityResult, edgeCandidates, threshold);
        if (candidates.Count == 0)
            return ContourTrainingEvaluation.Empty;

        if (scale < 1.0)
            candidates = MapCandidatesToOriginal(candidates, scale, sourceSize, templateSize);

        var matches = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, nmsOverlapThreshold);
        if (matches.Count == 0)
            return ContourTrainingEvaluation.Empty;

        var bestScore = matches.Max(match => match.Score);
        var templateContourScore = Math.Min(response.TemplateContourCount, 8) / 8.0;
        var matchCountScore = Math.Clamp(matches.Count / 4.0, 0, 1);
        var selectivityScore = matches.Count <= 12
            ? 1.0
            : 1.0 / Math.Sqrt(matches.Count / 12.0);
        var candidatePenalty = Math.Min(edgeCandidates.Count / 3000.0, 1.0);
        var coverageScore = Math.Clamp((settings.MinimumTemplateContourCoverage - 0.30) / 0.20, 0, 1);
        var thresholdScore = Math.Clamp((threshold - 0.30) / 0.40, 0, 1);
        var trainingScore =
            bestScore * 0.56 +
            templateContourScore * 0.10 +
            matchCountScore * 0.18 +
            selectivityScore * 0.08 +
            coverageScore * 0.04 +
            thresholdScore * 0.04 -
            candidatePenalty * 0.08;

        return new ContourTrainingEvaluation(
            settings,
            response.TemplateContourCount,
            candidates.Count,
            matches.Count,
            bestScore,
            trainingScore,
            threshold);
    }

    private static Mat BuildEdges(Mat image, ContourExtractionSettings settings)
    {
        using var gray = new Mat();
        if (image.Channels() == 1)
            image.CopyTo(gray);
        else
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

        using var normalized = new Mat();
        Cv2.Normalize(gray, normalized, 0, 255, NormTypes.MinMax);
        using var blurred = new Mat();
        Cv2.GaussianBlur(normalized, blurred, new Size(3, 3), 0);

        using var gradientKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var gradient = new Mat();
        Cv2.MorphologyEx(blurred, gradient, MorphTypes.Gradient, gradientKernel);
        using var gradientEdges = new Mat();
        var otsuThreshold = Cv2.Threshold(gradient, gradientEdges, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.Threshold(gradient, gradientEdges, otsuThreshold * settings.GradientThresholdScale, 255, ThresholdTypes.Binary);

        using var cannyEdges = new Mat();
        Cv2.Canny(blurred, cannyEdges, settings.CannyLowThreshold, settings.CannyHighThreshold);

        var edges = new Mat();
        Cv2.BitwiseOr(cannyEdges, gradientEdges, edges);
        if (settings.CloseIterations > 0)
        {
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(edges, edges, MorphTypes.Close, kernel, iterations: settings.CloseIterations);
        }

        return edges;
    }

    private static TemplateContourProfile CreateTemplateProfile(Size templateSize, Mat templateEdges, ContourExtractionSettings settings)
    {
        var contours = FindSignificantContours(templateEdges, settings)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Take(settings.MaximumTemplateContours)
            .Select(ContourDescriptor.Create)
            .ToList();

        if (contours.Count == 0)
            throw new InvalidOperationException("未找到模板轮廓。");

        return new TemplateContourProfile(templateSize, contours);
    }

    private static Mat CreateTemplateProfileEdges(Size templateSize, TemplateContourProfile templateProfile)
    {
        var profileEdges = new Mat(templateSize, MatType.CV_8UC1, Scalar.Black);
        Cv2.DrawContours(
            profileEdges,
            templateProfile.Contours.Select(contour => contour.Contour).ToArray(),
            -1,
            Scalar.White,
            1,
            LineTypes.AntiAlias);
        return profileEdges;
    }

    private static Point[][] FindSignificantContours(Mat edges, ContourExtractionSettings settings)
    {
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        // 先创建 ContourDescriptor（内部计算面积），再按面积过滤，消除双重 ContourArea P/Invoke
        var descriptors = new List<ContourDescriptor>(contours.Length);
        foreach (var contour in contours)
        {
            var desc = ContourDescriptor.Create(contour);
            if (desc.Area >= settings.MinimumContourArea)
                descriptors.Add(desc);
        }
        descriptors.Sort(static (a, b) => b.Area.CompareTo(a.Area));

        var selected = new List<ContourDescriptor>();
        foreach (var descriptor in descriptors)
        {
            var isDuplicate = false;
            for (var i = 0; i < selected.Count; i++)
            {
                if (CalculateIntersectionOverUnion(descriptor.Bounds, selected[i].Bounds) > DuplicateBoundsOverlapThreshold)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (isDuplicate)
                continue;

            selected.Add(descriptor);
        }

        var result = new Point[selected.Count][];
        for (var i = 0; i < selected.Count; i++)
            result[i] = selected[i].Contour;
        return result;
    }

    private static unsafe List<MatchCandidate> RescoreCandidatesByIntensity(
        Mat intensityResult,
        IReadOnlyList<MatchCandidate> candidates,
        double threshold)
    {
        if (candidates.Count == 0)
            return [];

        var verified = new List<MatchCandidate>(candidates.Count);
        var resultBounds = new Rect(0, 0, intensityResult.Width, intensityResult.Height);
        var intensityThreshold = CalculateIntensityVerificationThreshold(threshold);
        var step = (int)(intensityResult.Step() / sizeof(float));
        var ptr = (float*)intensityResult.DataPointer;

        foreach (var candidate in candidates)
        {
            if (!resultBounds.Contains(candidate.Rect.Location))
                continue;

            var intensityScore = ptr[candidate.Rect.Y * step + candidate.Rect.X];
            if (intensityScore < intensityThreshold)
                continue;

            var combinedScore = candidate.Score * 0.58 + intensityScore * 0.42;
            verified.Add(new MatchCandidate(candidate.Rect, combinedScore));
        }

        verified.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        return verified;
    }

    private static Mat PrepareGrayForVerification(Mat image)
    {
        var gray = new Mat();
        if (image.Channels() == 1)
            image.CopyTo(gray);
        else
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

        Cv2.GaussianBlur(gray, gray, new Size(3, 3), 0);
        return gray;
    }

    private static double CalculateIntensityVerificationThreshold(double threshold)
    {
        return Math.Clamp(threshold * 0.72, 0.42, 0.82);
    }

    private static List<MatchCandidate> FindEdgeResponseMatches(
        Mat sourceEdges,
        Mat templateEdges,
        TemplateContourProfile template,
        double threshold,
        ContourExtractionSettings settings,
        PerformanceProfile? profile = null)
    {
        var edgeCount = Cv2.CountNonZero(templateEdges);
        if (edgeCount == 0)
            return [];

        using var invertedSourceEdges = new Mat();
        Cv2.BitwiseNot(sourceEdges, invertedSourceEdges);
        using var distance = new Mat();
        Cv2.DistanceTransform(invertedSourceEdges, distance, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        profile?.Step("DistanceTransform");

        using var templateMask = new Mat();
        templateEdges.ConvertTo(templateMask, MatType.CV_32F, 1.0 / 255.0);
        using var distanceSum = new Mat();
        Cv2.MatchTemplate(distance, templateMask, distanceSum, TemplateMatchModes.CCorr);
        profile?.Step("MatchTemplate(距离)");

        using var sourceEdgeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var expandedSourceEdges = new Mat();
        Cv2.Dilate(sourceEdges, expandedSourceEdges, sourceEdgeKernel);
        using var sourceMask = new Mat();
        expandedSourceEdges.ConvertTo(sourceMask, MatType.CV_32F, 1.0 / 255.0);
        using var hitSum = new Mat();
        Cv2.MatchTemplate(sourceMask, templateMask, hitSum, TemplateMatchModes.CCorr);
        profile?.Step("MatchTemplate(命中)");

        return ExtractEdgeResponseCandidatesOptimized(
            distanceSum,
            hitSum,
            edgeCount,
            template.TemplateSize,
            threshold,
            settings.MinimumTemplateContourCoverage,
            settings.EdgeDistanceTolerance,
            profile);
    }

    /// <summary>
    /// Optimized candidate extraction using OpenCV matrix operations for score computation
    /// and dilate-based local maximum detection, replacing per-pixel P/Invoke loops.
    /// </summary>
    private static unsafe List<MatchCandidate> ExtractEdgeResponseCandidatesOptimized(
        Mat distanceSum,
        Mat hitSum,
        int edgeCount,
        Size templateSize,
        double threshold,
        double minimumTemplateContourCoverage,
        double edgeDistanceTolerance,
        PerformanceProfile? profile = null)
    {
        // ── Phase 1: Compute score matrix using OpenCV matrix operations ──
        // avgDist = distanceSum / edgeCount
        using var avgDist = new Mat();
        Cv2.Divide(distanceSum, new Scalar(edgeCount), avgDist);

        // distScore = 1.0 / (1.0 + avgDist / tolerance)
        //           = tolerance / (tolerance + avgDist)
        using var denominator = new Mat();
        Cv2.Add(avgDist, new Scalar(edgeDistanceTolerance), denominator);
        using var distScore = new Mat();
        Cv2.Divide(new Scalar(edgeDistanceTolerance), denominator, distScore);

        // hitRatio = clamp(hitSum / edgeCount, 0, 1)
        using var hitRatio = new Mat();
        Cv2.Divide(hitSum, new Scalar(edgeCount), hitRatio);
        using var hitRatioClamped = new Mat();
        Cv2.Min(hitRatio, new Scalar(1.0), hitRatioClamped);
        using var hitRatioFinal = new Mat();
        Cv2.Max(hitRatioClamped, new Scalar(0.0), hitRatioFinal);

        // scores = distScore * 0.72 + hitRatioFinal * 0.28
        using var scores = new Mat();
        Cv2.AddWeighted(distScore, 0.72, hitRatioFinal, 0.28, 0, scores);
        profile?.Step("ScoreMatrix(矩阵运算)", $"{scores.Cols}×{scores.Rows}");

        // ── Phase 2: Determine effective threshold ──
        double effectiveThreshold;
        if (threshold > 0)
        {
            effectiveThreshold = threshold;
        }
        else
        {
            Cv2.MinMaxLoc(scores, out _, out var maxScore, out _, out _);
            effectiveThreshold = Math.Max(0.05, maxScore * 0.78);
        }

        // ── Phase 3: Local maximum detection via dilate ──
        using var kernel3x3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var dilated = new Mat();
        Cv2.Dilate(scores, dilated, kernel3x3);

        // localMaxMask = (scores == dilated) AND (scores >= threshold)
        using var isMax = new Mat();
        Cv2.Compare(scores, dilated, isMax, CmpTypes.EQ);

        using var aboveThreshold = new Mat();
        using var thresholdMat = new Mat(scores.Rows, scores.Cols, MatType.CV_32F, new Scalar(effectiveThreshold));
        using var aboveThresholdRaw = new Mat();
        Cv2.Compare(scores, thresholdMat, aboveThresholdRaw, CmpTypes.GE);

        using var coverageThresholdMat = new Mat(scores.Rows, scores.Cols, MatType.CV_32F, new Scalar(minimumTemplateContourCoverage));
        using var aboveCoverageRaw = new Mat();
        Cv2.Compare(hitRatioFinal, coverageThresholdMat, aboveCoverageRaw, CmpTypes.GE);

        using var localMaxMask = new Mat();
        Cv2.BitwiseAnd(isMax, aboveThresholdRaw, localMaxMask);
        Cv2.BitwiseAnd(localMaxMask, aboveCoverageRaw, localMaxMask);

        // Exclude border pixels (y=0, y=last, x=0, x=last) to match original behavior
        var rows = scores.Rows;
        var cols = scores.Cols;
        if (rows > 2 && cols > 2)
        {
            localMaxMask.Row(0).SetTo(new Scalar(0));
            localMaxMask.Row(rows - 1).SetTo(new Scalar(0));
            localMaxMask.Col(0).SetTo(new Scalar(0));
            localMaxMask.Col(cols - 1).SetTo(new Scalar(0));
        }

        // ── Phase 4: Extract candidate locations ──
        using var locations = new Mat();
        Cv2.FindNonZero(localMaxMask, locations);
        profile?.Step("LocalMax(Dilate+Compare)", locations.Empty() ? "0 点" : $"{locations.Rows} 点");

        if (locations.Empty())
            return [];

        var locationCount = locations.Rows;
        var tw = templateSize.Width;
        var th = templateSize.Height;
        var scoreStep = (int)(scores.Step() / sizeof(float));
        var scorePtr = (float*)scores.DataPointer;

        var matches = new List<MatchCandidate>(locationCount);
        var locPtr = (Point*)locations.DataPointer;
        for (var i = 0; i < locationCount; i++)
        {
            var pt = locPtr[i];
            var score = scorePtr[pt.Y * scoreStep + pt.X];
            matches.Add(new MatchCandidate(new Rect(pt.X, pt.Y, tw, th), score));
        }

        matches.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        if (matches.Count > 3000)
            matches.RemoveRange(3000, matches.Count - 3000);
        return matches;
    }

    private static double CalculateIntersectionOverUnion(Rect a, Rect b)
    {
        var intersection = a.Intersect(b);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0;

        var intersectionArea = intersection.Width * intersection.Height;
        var unionArea = a.Width * a.Height + b.Width * b.Height - intersectionArea;
        return unionArea <= 0 ? 0 : (double)intersectionArea / unionArea;
    }

}
