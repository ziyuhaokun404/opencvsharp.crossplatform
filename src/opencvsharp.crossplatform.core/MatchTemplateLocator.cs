using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core.Image;
using OpenCvSharp.CrossPlatform.Core.Profiling;
using OpenCvSharp.CrossPlatform.Core.Selection;

namespace OpenCvSharp.CrossPlatform.Core.Matching;

public sealed class MatchTemplateLocator : ITemplateLocator, IDisposable
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
    private const int MaxFullResolutionRefineTemplatePixels = 1_048_576;
    private const int MaxPerCandidateRefineTemplatePixels = 262_144;
    private const int MaxScaledRefineTemplatePixels = 4_096;
    private const int MaxFullResolutionRefineCandidates = 64;
    private const int DenseMatchRefineCandidates = 16;
    private const int DenseMatchThreshold = 512;
    private const int VeryDenseMatchRefineCandidates = 8;
    private const int VeryDenseMatchThreshold = 2048;
    private const int MaxAngleEstimateCandidates = 128;
    private const int MaxAngleEstimatePixels = 4096;
    private const double MinAngleEstimateAspectRatio = 1.25;
    private const int MaxRefinementPatchCount = 3;
    private const int MaxRefinementPatchEdge = 48;
    private const int FinalPatchRefineRadiusPixels = 4;
    private const int PatchAgreementTolerancePixels = 2;
    private const double MaxRefinementPatchOverlap = 0.20;
    private const double RefineRadiusPyramidPixels = 2.0;
    private const int MinRefineRadiusPixels = 6;
    private const int MaxRefineRadiusPixels = 32;
    private TemplateRefinementCache? refinementCache;

    public string Name => "模板匹配";

    public void Dispose()
    {
        refinementCache?.Dispose();
        refinementCache = null;
    }

    private static int GetMaxRefineCandidates(TemplateLocatorOptions options, int matchCount)
    {
        if (options.MaxRefineCandidates > 0)
            return options.MaxRefineCandidates;

        if (!options.PreserveUnrefinedMatches)
            return MaxFullResolutionRefineCandidates;

        if (matchCount >= VeryDenseMatchThreshold)
            return VeryDenseMatchRefineCandidates;

        if (matchCount >= DenseMatchThreshold)
            return DenseMatchRefineCandidates;

        return MaxFullResolutionRefineCandidates;
    }

    private static int CreatePreservedMatchCapacity(int matchCount, int refinedLimit, TemplateLocatorOptions options)
    {
        var capacity = options.PreserveUnrefinedMatches ? matchCount + 1 : refinedLimit + 1;
        if (options.MaxMatches > 0)
            capacity = Math.Min(capacity, options.MaxMatches + 1);

        return Math.Max(0, capacity);
    }

    private static void AddUnrefinedMatches(
        List<MatchCandidate> target,
        IReadOnlyList<MatchCandidate> matches,
        int startIndex,
        TemplateLocatorOptions options)
    {
        if (!options.PreserveUnrefinedMatches)
            return;

        var refinedCount = target.Count;
        var remaining = options.MaxMatches > 0
            ? Math.Max(0, options.MaxMatches - target.Count)
            : int.MaxValue;
        for (var i = startIndex; i < matches.Count && remaining > 0; i++)
        {
            if (IsSuppressedByExisting(matches[i], target, refinedCount, options.NmsOverlapThreshold))
                continue;

            target.Add(matches[i]);
            remaining--;
        }
    }

    private static List<MatchCandidate> LimitMatches(List<MatchCandidate> matches, int maxMatches)
    {
        if (maxMatches <= 0 || matches.Count <= maxMatches)
            return matches;

        return matches.GetRange(0, maxMatches);
    }

    private static bool IsSuppressedByExisting(
        MatchCandidate candidate,
        IReadOnlyList<MatchCandidate> existing,
        int existingCount,
        double overlapThreshold)
    {
        for (var i = 0; i < existingCount; i++)
        {
            if (MatchCandidateUtilities.CalculateIntersectionOverUnion(candidate.Rect, existing[i].Rect) > overlapThreshold)
                return true;
        }

        return false;
    }

    public TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options)
    {
        var profile = new PerformanceProfile($"模板匹配 ({source.Width}×{source.Height}, 模板 {template.Width}×{template.Height})");
        var stopwatch = Stopwatch.StartNew();

        // 仅灰度转换时分配新 Mat；彩色模式直接使用原 Mat，不 Clone
        using var graySource = options.UseGrayscale ? ImageHelpers.ConvertToGray(source) : null;
        using var grayTemplate = options.UseGrayscale ? ImageHelpers.ConvertToGray(template) : null;
        var matchSource = graySource ?? source;
        var matchTemplate = grayTemplate ?? template;
        profile.Step("PrepareForMatching", options.UseGrayscale ? "灰度" : "直接（无 Clone）");

        if (matchTemplate.Width > matchSource.Width || matchTemplate.Height > matchSource.Height)
            throw new InvalidOperationException("模板尺寸不能大于源图像。");

        var resultCols = matchSource.Width - matchTemplate.Width + 1;
        var resultRows = matchSource.Height - matchTemplate.Height + 1;
        var resultArea = (long)resultCols * resultRows;
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

            var coarseBestCandidate = new MatchCandidate(new Rect(bestLocation, template.Size()), bestScore);
            var bestCandidate = coarseBestCandidate;
            if (usedPyramid)
            {
                bestCandidate = RefineBestCandidate(matchSource, matchTemplate, bestCandidate, options, scale, profile);
                matches = RefineMatches(matchSource, matchTemplate, matches, coarseBestCandidate, bestCandidate, options, scale, profile);
            }

            matches = LimitMatches(matches, options.MaxMatches);
            matches = EstimateMatchAngles(matchSource, matches, template.Size(), profile);
            bestCandidate = bestCandidate with { Angle = ShouldEstimateAngles(template.Size()) ? EstimateMatchAngle(matchSource, bestCandidate.Rect) : 0 };

            stopwatch.Stop();
            var profileResult = profile.Finish();

            return new TemplateLocatorResult(
                bestCandidate.Rect.Location,
                bestCandidate.Score,
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

    private static List<MatchCandidate> EstimateMatchAngles(
        Mat source,
        List<MatchCandidate> matches,
        Size templateSize,
        PerformanceProfile profile)
    {
        if (matches.Count == 0)
            return matches;

        if (!ShouldEstimateAngles(templateSize))
        {
            profile.Step("AngleEstimate", "跳过：模板宽高比不足");
            return matches;
        }

        var angled = new List<MatchCandidate>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var angle = i < MaxAngleEstimateCandidates
                ? EstimateMatchAngle(source, match.Rect)
                : 0;
            angled.Add(match with { Angle = angle });
        }

        var detail = matches.Count <= MaxAngleEstimateCandidates
            ? $"匹配 {matches.Count}"
            : $"候选 {MaxAngleEstimateCandidates}/{matches.Count}";
        profile.Step("AngleEstimate", detail);
        return angled;
    }

    private static bool ShouldEstimateAngles(Size templateSize)
    {
        var minEdge = Math.Max(1, Math.Min(templateSize.Width, templateSize.Height));
        var maxEdge = Math.Max(templateSize.Width, templateSize.Height);
        return (double)maxEdge / minEdge >= MinAngleEstimateAspectRatio;
    }

    private static double EstimateMatchAngle(Mat source, Rect rect)
    {
        var bounds = new Rect(0, 0, source.Width, source.Height);
        var roiRect = rect.Intersect(bounds);
        if (roiRect.Width < 4 || roiRect.Height < 4)
            return 0;

        using var roi = new Mat(source, roiRect);
        var scale = ComputeAngleEstimateScale(roiRect.Width, roiRect.Height);
        using var resized = scale < 1.0 ? new Mat() : null;
        var angleSource = roi;
        if (resized is not null)
        {
            Cv2.Resize(roi, resized, new Size(0, 0), scale, scale, InterpolationFlags.Area);
            angleSource = resized;
        }

        using var gray = angleSource.Channels() == 1 ? angleSource : ImageHelpers.ConvertToGray(angleSource);
        if (gray.Type() != MatType.CV_8UC1)
            return 0;

        return EstimateGrayPrincipalAngle(gray);
    }

    private static double ComputeAngleEstimateScale(int width, int height)
    {
        var area = (long)width * height;
        if (area <= MaxAngleEstimatePixels)
            return 1.0;

        return Math.Sqrt((double)MaxAngleEstimatePixels / area);
    }

    private static unsafe double EstimateGrayPrincipalAngle(Mat gray)
    {
        var width = gray.Width;
        var height = gray.Height;
        var ptr = (byte*)gray.DataPointer;
        var step = (long)gray.Step();
        var sum = 0.0;
        var pixels = width * height;
        for (var y = 0; y < height; y++)
        {
            var row = ptr + y * step;
            for (var x = 0; x < width; x++)
                sum += row[x];
        }

        var mean = sum / pixels;
        var m00 = 0.0;
        var m10 = 0.0;
        var m01 = 0.0;
        for (var y = 0; y < height; y++)
        {
            var row = ptr + y * step;
            for (var x = 0; x < width; x++)
            {
                var weight = Math.Abs(row[x] - mean);
                if (weight < 8)
                    continue;

                m00 += weight;
                m10 += weight * x;
                m01 += weight * y;
            }
        }

        if (m00 <= 0)
            return 0;

        var centerX = m10 / m00;
        var centerY = m01 / m00;
        var mu20 = 0.0;
        var mu02 = 0.0;
        var mu11 = 0.0;
        for (var y = 0; y < height; y++)
        {
            var row = ptr + y * step;
            var dy = y - centerY;
            for (var x = 0; x < width; x++)
            {
                var weight = Math.Abs(row[x] - mean);
                if (weight < 8)
                    continue;

                var dx = x - centerX;
                mu20 += weight * dx * dx;
                mu02 += weight * dy * dy;
                mu11 += weight * dx * dy;
            }
        }

        var angle = 0.5 * Math.Atan2(2.0 * mu11, mu20 - mu02) * 180.0 / Math.PI;
        return NormalizeAngle(angle);
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle <= -90) angle += 180;
        while (angle > 90) angle -= 180;
        return angle;
    }

    /// <summary>
    /// Computes the downsampling scale factor. Returns 1.0 if no pyramid is needed.
    /// </summary>
    private static double ComputePyramidScale(long resultArea, int templateWidth, int templateHeight)
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

    private static MatchCandidate RefineBestCandidate(
        Mat source,
        Mat template,
        MatchCandidate bestCandidate,
        TemplateLocatorOptions options,
        double scale,
        PerformanceProfile profile)
    {
        if ((long)template.Width * template.Height > MaxFullResolutionRefineTemplatePixels)
        {
            profile.Step("FullResolution.RefineBest", $"跳过：模板超过 {MaxFullResolutionRefineTemplatePixels} px");
            return bestCandidate;
        }

        var radius = ComputeRefineRadius(scale);
        var refined = RefineCandidate(source, template, bestCandidate.Rect.Location, radius, options);
        profile.Step("FullResolution.RefineBest", $"半径 {radius}px，{bestCandidate.Rect.Location} → {refined.Rect.Location}");
        return refined;
    }

    private List<MatchCandidate> RefineMatches(
        Mat source,
        Mat template,
        List<MatchCandidate> matches,
        MatchCandidate coarseBestCandidate,
        MatchCandidate refinedBestCandidate,
        TemplateLocatorOptions options,
        double scale,
        PerformanceProfile profile)
    {
        if ((long)template.Width * template.Height > MaxFullResolutionRefineTemplatePixels)
        {
            profile.Step("FullResolution.RefineMatches", "跳过");
            return matches;
        }

        if ((long)template.Width * template.Height > MaxPerCandidateRefineTemplatePixels)
            return RefineLargeTemplateMatches(source, template, matches, coarseBestCandidate, refinedBestCandidate, options, scale, profile);

        var limit = Math.Min(matches.Count, GetMaxRefineCandidates(options, matches.Count));
        var radius = ComputeRefineRadius(scale);
        var refined = new List<MatchCandidate>(CreatePreservedMatchCapacity(matches.Count, limit, options));
        for (var i = 0; i < limit; i++)
        {
            var candidate = RefineCandidate(source, template, matches[i].Rect.Location, radius, options);
            if (candidate.Score >= options.Threshold)
                refined.Add(candidate);
        }

        AddUnrefinedMatches(refined, matches, limit, options);

        if (refinedBestCandidate.Score >= options.Threshold &&
            !refined.Any(candidate => candidate.Rect == refinedBestCandidate.Rect))
        {
            refined.Add(refinedBestCandidate);
        }

        if (refined.Count == 0)
        {
            profile.Step("FullResolution.RefineMatches", $"候选 {limit}/{matches.Count}，无阈值内命中");
            return matches;
        }

        var refinedMatches = LimitMatches(MatchCandidateUtilities.ApplyNonMaximumSuppression(refined, options.NmsOverlapThreshold), options.MaxMatches);
        var preservedDetail = options.PreserveUnrefinedMatches ? $"，保留粗匹配 {Math.Max(0, matches.Count - limit)}" : "";
        profile.Step("FullResolution.RefineMatches", $"候选 {limit}/{matches.Count}{preservedDetail}，匹配 {refinedMatches.Count}");
        return refinedMatches;
    }

    private List<MatchCandidate> RefineLargeTemplateMatches(
        Mat source,
        Mat template,
        List<MatchCandidate> matches,
        MatchCandidate coarseBestCandidate,
        MatchCandidate refinedBestCandidate,
        TemplateLocatorOptions options,
        double scale,
        PerformanceProfile profile)
    {
        var limit = Math.Min(matches.Count, GetMaxRefineCandidates(options, matches.Count));
        var hasNonBestCandidate = false;
        for (var i = 0; i < limit; i++)
        {
            if (matches[i].Rect == coarseBestCandidate.Rect)
                continue;

            hasNonBestCandidate = true;
            break;
        }

        if (!hasNonBestCandidate)
        {
            var bestOnly = new List<MatchCandidate>(1) { refinedBestCandidate };
            var bestOnlyMatches = MatchCandidateUtilities.ApplyNonMaximumSuppression(bestOnly, options.NmsOverlapThreshold);
            profile.Step("FullResolution.RefineMatches", $"仅最佳候选，匹配 {bestOnlyMatches.Count}");
            return bestOnlyMatches;
        }

        var radius = ComputeRefineRadius(scale);
        var cache = GetOrCreateRefinementCache(template);
        var refined = new List<MatchCandidate>(CreatePreservedMatchCapacity(matches.Count, limit, options));
        var totalPatchRefinements = 0;
        var patchRefinedCandidates = 0;
        var scaledFallbacks = 0;

        for (var i = 0; i < limit; i++)
        {
            var match = matches[i];
            MatchCandidate candidate;
            if (match.Rect == coarseBestCandidate.Rect)
            {
                candidate = refinedBestCandidate;
            }
            else
            {
                candidate = RefineCandidateWithPatches(
                    source,
                    cache.Patches,
                    template.Size(),
                    match,
                    radius,
                    options,
                    out var usedPatchRefinements,
                    out var isConfident);
                totalPatchRefinements += usedPatchRefinements;
                patchRefinedCandidates++;
                if (!isConfident)
                {
                    var scaledCandidate = RefineCandidateWithScaledTemplate(
                        source,
                        cache.GetOrCreateScaledTemplate(template),
                        template.Size(),
                        match,
                        radius,
                        options);
                    candidate = RefineCandidateWithPatches(
                        source,
                        cache.Patches,
                        template.Size(),
                        scaledCandidate,
                        FinalPatchRefineRadiusPixels,
                        options,
                        out var fallbackPatchRefinements,
                        out _);
                    totalPatchRefinements += fallbackPatchRefinements;
                    scaledFallbacks++;
                }
            }

            refined.Add(candidate);
        }

        AddUnrefinedMatches(refined, matches, limit, options);

        if (refinedBestCandidate.Score >= options.Threshold &&
            !refined.Any(candidate => candidate.Rect == refinedBestCandidate.Rect))
        {
            refined.Add(refinedBestCandidate);
        }

        var refinedMatches = LimitMatches(MatchCandidateUtilities.ApplyNonMaximumSuppression(refined, options.NmsOverlapThreshold), options.MaxMatches);
        var fallbackDetail = scaledFallbacks == 0
            ? "无缩略回退"
            : $"缩略回退 {scaledFallbacks} 次 ×{cache.ScaledTemplateScale:F2}";
        var preservedDetail = options.PreserveUnrefinedMatches ? $"，保留粗匹配 {Math.Max(0, matches.Count - limit)}" : "";
        profile.Step(
            "FullResolution.RefineMatches",
            $"锚点直校 {cache.Patches.Count} 个，实际 {totalPatchRefinements}/{Math.Max(1, patchRefinedCandidates * cache.Patches.Count)}，{fallbackDetail}，候选 {limit}/{matches.Count}{preservedDetail}，匹配 {refinedMatches.Count}");
        return refinedMatches;
    }

    private TemplateRefinementCache GetOrCreateRefinementCache(Mat template)
    {
        var fingerprint = ComputeTemplateFingerprint(template);
        if (refinementCache is not null && refinementCache.Matches(template, fingerprint))
            return refinementCache;

        refinementCache?.Dispose();
        refinementCache = new TemplateRefinementCache(
            template.Width,
            template.Height,
            template.Type(),
            fingerprint,
            CreateRefinementPatches(template));
        return refinementCache;
    }

    private static unsafe ulong ComputeTemplateFingerprint(Mat template)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        hash = MixHash(hash, (ulong)template.Width);
        hash = MixHash(hash, (ulong)template.Height);
        hash = MixHash(hash, (ulong)template.Type().GetHashCode());

        var ptr = (byte*)template.DataPointer;
        var step = (long)template.Step();
        var rowBytes = checked(template.Width * (int)template.ElemSize());
        for (var y = 0; y < template.Height; y++)
        {
            var row = ptr + y * step;
            for (var x = 0; x < rowBytes; x++)
            {
                hash ^= row[x];
                hash *= prime;
            }
        }

        return hash;

        static ulong MixHash(ulong hash, ulong value)
        {
            const ulong prime = 1099511628211UL;
            for (var i = 0; i < sizeof(ulong); i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= prime;
            }

            return hash;
        }
    }

    private static MatchCandidate RefineCandidateWithScaledTemplate(
        Mat source,
        ScaledTemplateRefinement scaledTemplate,
        Size templateSize,
        MatchCandidate candidate,
        int radius,
        TemplateLocatorOptions options)
    {
        var maxX = source.Width - templateSize.Width;
        var maxY = source.Height - templateSize.Height;
        var fromX = Math.Clamp(candidate.Rect.X - radius, 0, maxX);
        var fromY = Math.Clamp(candidate.Rect.Y - radius, 0, maxY);
        var toX = Math.Clamp(candidate.Rect.X + radius, 0, maxX);
        var toY = Math.Clamp(candidate.Rect.Y + radius, 0, maxY);
        var roi = new Rect(fromX, fromY, templateSize.Width + toX - fromX, templateSize.Height + toY - fromY);

        using var sourceRoi = new Mat(source, roi);
        using var scaledSourceRoi = new Mat();
        Cv2.Resize(sourceRoi, scaledSourceRoi, new Size(0, 0), scaledTemplate.Scale, scaledTemplate.Scale, InterpolationFlags.Area);
        if (scaledSourceRoi.Width < scaledTemplate.Template.Width || scaledSourceRoi.Height < scaledTemplate.Template.Height)
            return candidate;

        using var result = new Mat();
        Cv2.MatchTemplate(scaledSourceRoi, scaledTemplate.Template, result, options.Method);
        Cv2.MinMaxLoc(result, out _, out _, out var minLocation, out var maxLocation);

        var bestLocal = options.HigherIsBetter ? maxLocation : minLocation;
        var x = Math.Clamp(fromX + (int)Math.Round(bestLocal.X / scaledTemplate.Scale), 0, maxX);
        var y = Math.Clamp(fromY + (int)Math.Round(bestLocal.Y / scaledTemplate.Scale), 0, maxY);
        return new MatchCandidate(new Rect(x, y, templateSize.Width, templateSize.Height), candidate.Score);
    }

    private static MatchCandidate RefineCandidateWithPatches(
        Mat source,
        RefinementPatchSet patches,
        Size templateSize,
        MatchCandidate candidate,
        int radius,
        TemplateLocatorOptions options,
        out int usedPatchRefinements,
        out bool isConfident)
    {
        if (patches.Count == 0)
        {
            usedPatchRefinements = 0;
            isConfident = false;
            return candidate;
        }

        var patchResult = RefineLocationWithPatches(source, patches, templateSize, candidate.Rect.Location, radius, options);
        usedPatchRefinements = patchResult.UsedPatchRefinements;
        isConfident = patchResult.IsConfident;
        return new MatchCandidate(new Rect(patchResult.Location, templateSize), candidate.Score);
    }

    private static PatchRefinementResult RefineLocationWithPatches(
        Mat source,
        RefinementPatchSet patches,
        Size templateSize,
        Point initialLocation,
        int radius,
        TemplateLocatorOptions options)
    {
        if (patches.Count == 0)
            return new PatchRefinementResult(initialLocation, 0, false);

        var first = RefineLocationWithPatch(source, patches.Patches[0], templateSize, initialLocation, radius, options);
        if (patches.Count == 1)
            return new PatchRefinementResult(first, 1, false);

        var second = RefineLocationWithPatch(source, patches.Patches[1], templateSize, initialLocation, radius, options);
        if (AreLocationsClose(first, second, PatchAgreementTolerancePixels))
            return new PatchRefinementResult(AverageLocation(first, second), 2, true);

        var locations = new List<Point>(patches.Count);
        locations.Add(first);
        locations.Add(second);
        for (var i = 2; i < patches.Count; i++)
        {
            var patch = patches.Patches[i];
            locations.Add(RefineLocationWithPatch(source, patch, templateSize, initialLocation, radius, options));
        }

        var location = SelectConsensusLocation(locations);
        var isConfident = CountCloseLocations(locations, location, PatchAgreementTolerancePixels) >= 2;
        return new PatchRefinementResult(location, locations.Count, isConfident);
    }

    private static Point RefineLocationWithPatch(
        Mat source,
        RefinementPatch patch,
        Size templateSize,
        Point initialLocation,
        int radius,
        TemplateLocatorOptions options)
    {
        var expectedPatchX = initialLocation.X + patch.Offset.X;
        var expectedPatchY = initialLocation.Y + patch.Offset.Y;
        var maxPatchX = source.Width - patch.Template.Width;
        var maxPatchY = source.Height - patch.Template.Height;
        var fromX = Math.Clamp(expectedPatchX - radius, 0, maxPatchX);
        var fromY = Math.Clamp(expectedPatchY - radius, 0, maxPatchY);
        var toX = Math.Clamp(expectedPatchX + radius, 0, maxPatchX);
        var toY = Math.Clamp(expectedPatchY + radius, 0, maxPatchY);
        var roi = new Rect(fromX, fromY, patch.Template.Width + toX - fromX, patch.Template.Height + toY - fromY);

        using var sourceRoi = new Mat(source, roi);
        using var result = new Mat();
        Cv2.MatchTemplate(sourceRoi, patch.Template, result, options.Method);
        Cv2.MinMaxLoc(result, out _, out _, out var minLocation, out var maxLocation);

        var bestLocal = options.HigherIsBetter ? maxLocation : minLocation;
        var maxTemplateX = Math.Max(0, source.Width - templateSize.Width);
        var maxTemplateY = Math.Max(0, source.Height - templateSize.Height);
        var x = Math.Clamp(fromX + bestLocal.X - patch.Offset.X, 0, maxTemplateX);
        var y = Math.Clamp(fromY + bestLocal.Y - patch.Offset.Y, 0, maxTemplateY);
        return new Point(x, y);
    }

    private static Point SelectConsensusLocation(List<Point> locations)
    {
        if (locations.Count == 1)
            return locations[0];

        var bestIndex = 0;
        var bestDistance = long.MaxValue;
        for (var i = 0; i < locations.Count; i++)
        {
            long distance = 0;
            for (var j = 0; j < locations.Count; j++)
            {
                distance += Math.Abs(locations[i].X - locations[j].X);
                distance += Math.Abs(locations[i].Y - locations[j].Y);
            }

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return locations[bestIndex];
    }

    private static bool AreLocationsClose(Point a, Point b, int tolerance)
    {
        return Math.Abs(a.X - b.X) <= tolerance &&
            Math.Abs(a.Y - b.Y) <= tolerance;
    }

    private static Point AverageLocation(Point a, Point b)
    {
        return new Point(
            (int)Math.Round((a.X + b.X) / 2.0),
            (int)Math.Round((a.Y + b.Y) / 2.0));
    }

    private static int CountCloseLocations(List<Point> locations, Point location, int tolerance)
    {
        var count = 0;
        for (var i = 0; i < locations.Count; i++)
        {
            if (AreLocationsClose(locations[i], location, tolerance))
                count++;
        }

        return count;
    }

    private static MatchCandidate RefineCandidate(
        Mat source,
        Mat template,
        Point initialLocation,
        int radius,
        TemplateLocatorOptions options)
    {
        var maxX = source.Width - template.Width;
        var maxY = source.Height - template.Height;
        var fromX = Math.Clamp(initialLocation.X - radius, 0, maxX);
        var fromY = Math.Clamp(initialLocation.Y - radius, 0, maxY);
        var toX = Math.Clamp(initialLocation.X + radius, 0, maxX);
        var toY = Math.Clamp(initialLocation.Y + radius, 0, maxY);
        var roi = new Rect(fromX, fromY, template.Width + toX - fromX, template.Height + toY - fromY);

        using var sourceRoi = new Mat(source, roi);
        using var result = new Mat();
        Cv2.MatchTemplate(sourceRoi, template, result, options.Method);
        Cv2.MinMaxLoc(result, out var minValue, out var maxValue, out var minLocation, out var maxLocation);

        var bestLocal = options.HigherIsBetter ? maxLocation : minLocation;
        var score = options.HigherIsBetter ? maxValue : 1.0 - minValue;
        return new MatchCandidate(
            new Rect(fromX + bestLocal.X, fromY + bestLocal.Y, template.Width, template.Height),
            score);
    }

    private static int ComputeRefineRadius(double scale)
    {
        if (scale >= 1.0)
            return MinRefineRadiusPixels;

        var radius = (int)Math.Ceiling(RefineRadiusPyramidPixels / scale);
        return Math.Clamp(radius, MinRefineRadiusPixels, MaxRefineRadiusPixels);
    }

    private static ScaledTemplateRefinement CreateScaledTemplateRefinement(Mat template)
    {
        var templateArea = (long)template.Width * template.Height;
        var scale = Math.Min(1.0, Math.Sqrt((double)MaxScaledRefineTemplatePixels / templateArea));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(0, 0), scale, scale, InterpolationFlags.Area);
        return new ScaledTemplateRefinement(scaledTemplate, scale);
    }

    private static RefinementPatchSet CreateRefinementPatches(Mat template)
    {
        var patchWidth = Math.Min(template.Width, MaxRefinementPatchEdge);
        var patchHeight = Math.Min(template.Height, MaxRefinementPatchEdge);
        var xPositions = CreatePatchPositions(template.Width, patchWidth);
        var yPositions = CreatePatchPositions(template.Height, patchHeight);
        var candidates = new List<RefinementPatchCandidate>(xPositions.Length * yPositions.Length);

        foreach (var y in yPositions)
        {
            foreach (var x in xPositions)
            {
                var rect = new Rect(x, y, patchWidth, patchHeight);
                using var patch = new Mat(template, rect);
                var score = CalculatePatchScore(patch);
                candidates.Add(new RefinementPatchCandidate(rect, score));
            }
        }

        candidates.Sort(static (a, b) =>
        {
            if (b.Score > a.Score) return 1;
            if (b.Score < a.Score) return -1;
            return 0;
        });

        var selected = new List<RefinementPatch>(MaxRefinementPatchCount);
        foreach (var candidate in candidates)
        {
            if (selected.Count > 0 && selected.Any(patch => CalculatePatchOverlap(candidate.Rect, patch.Rect) > MaxRefinementPatchOverlap))
                continue;

            selected.Add(new RefinementPatch(new Mat(template, candidate.Rect).Clone(), candidate.Rect.Location));
            if (selected.Count == MaxRefinementPatchCount)
                break;
        }

        if (selected.Count == 0)
        {
            var fallbackRect = new Rect((template.Width - patchWidth) / 2, (template.Height - patchHeight) / 2, patchWidth, patchHeight);
            selected.Add(new RefinementPatch(new Mat(template, fallbackRect).Clone(), fallbackRect.Location));
        }

        return new RefinementPatchSet(selected);
    }

    private static int[] CreatePatchPositions(int length, int patchLength)
    {
        var max = length - patchLength;
        if (max <= 0)
            return [0];

        var values = new[] { 0, max / 4, max / 2, max * 3 / 4, max };
        return values.Distinct().OrderBy(value => value).ToArray();
    }

    private static double CalculatePatchScore(Mat patch)
    {
        Cv2.MeanStdDev(patch, out _, out var stddev);
        return stddev.Val0 + stddev.Val1 + stddev.Val2 + stddev.Val3;
    }

    private static double CalculatePatchOverlap(Rect a, Rect b)
    {
        var intersectionX1 = Math.Max(a.X, b.X);
        var intersectionY1 = Math.Max(a.Y, b.Y);
        var intersectionX2 = Math.Min(a.Right, b.Right);
        var intersectionY2 = Math.Min(a.Bottom, b.Bottom);
        var intersectionWidth = Math.Max(0, intersectionX2 - intersectionX1);
        var intersectionHeight = Math.Max(0, intersectionY2 - intersectionY1);
        var intersectionArea = intersectionWidth * intersectionHeight;
        if (intersectionArea == 0)
            return 0.0;

        var unionArea = a.Width * a.Height + b.Width * b.Height - intersectionArea;
        return (double)intersectionArea / unionArea;
    }

    private sealed class ScaledTemplateRefinement : IDisposable
    {
        public ScaledTemplateRefinement(Mat template, double scale)
        {
            Template = template;
            Scale = scale;
        }

        public Mat Template { get; }

        public double Scale { get; }

        public void Dispose()
        {
            Template.Dispose();
        }
    }

    private sealed class TemplateRefinementCache : IDisposable
    {
        public TemplateRefinementCache(
            int width,
            int height,
            MatType type,
            ulong fingerprint,
            RefinementPatchSet patches)
        {
            Width = width;
            Height = height;
            Type = type;
            Fingerprint = fingerprint;
            Patches = patches;
        }

        private int Width { get; }

        private int Height { get; }

        private MatType Type { get; }

        private ulong Fingerprint { get; }

        public RefinementPatchSet Patches { get; }

        private ScaledTemplateRefinement? ScaledTemplate { get; set; }

        public double ScaledTemplateScale => ScaledTemplate?.Scale ?? 1.0;

        public ScaledTemplateRefinement GetOrCreateScaledTemplate(Mat template)
        {
            ScaledTemplate ??= CreateScaledTemplateRefinement(template);
            return ScaledTemplate;
        }

        public bool Matches(Mat template, ulong fingerprint)
        {
            return Width == template.Width &&
                Height == template.Height &&
                Type == template.Type() &&
                Fingerprint == fingerprint;
        }

        public void Dispose()
        {
            ScaledTemplate?.Dispose();
            Patches.Dispose();
        }
    }

    private sealed class RefinementPatchSet : IDisposable
    {
        public RefinementPatchSet(IReadOnlyList<RefinementPatch> patches)
        {
            Patches = patches;
        }

        public IReadOnlyList<RefinementPatch> Patches { get; }

        public int Count => Patches.Count;

        public void Dispose()
        {
            foreach (var patch in Patches)
                patch.Dispose();
        }
    }

    private sealed class RefinementPatch : IDisposable
    {
        public RefinementPatch(Mat template, Point offset)
        {
            Template = template;
            Offset = offset;
            Rect = new Rect(offset, template.Size());
        }

        public Mat Template { get; }

        public Point Offset { get; }

        public Rect Rect { get; }

        public void Dispose()
        {
            Template.Dispose();
        }
    }

    private readonly record struct RefinementPatchCandidate(Rect Rect, double Score);

    private readonly record struct PatchRefinementResult(Point Location, int UsedPatchRefinements, bool IsConfident);

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
