using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenCvSharp;

namespace OpenCvSharp.Mac.Core;

public sealed class CoarseToFineMatchTemplateLocator : ITemplateLocator
{
    private const int CoarseResultTargetPixels = 50_000;
    private const int MinCoarseTemplateEdge = 4;
    private const int MaxCoarseCandidates = 128;
    private const int MaxFineCandidates = 16;
    private const int MaxFullResolutionRefineTemplatePixels = 65_536;
    private const double RefineRadiusCoarsePixels = 3.0;
    private const int MinRefineRadiusPixels = 8;

    public string Name => "粗到细模板匹配";

    public TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options)
    {
        var profile = new PerformanceProfile($"粗到细模板匹配 ({source.Width}×{source.Height}, 模板 {template.Width}×{template.Height})");
        var stopwatch = Stopwatch.StartNew();

        using var graySource = options.UseGrayscale ? ImageHelpers.ConvertToGray(source) : null;
        using var grayTemplate = options.UseGrayscale ? ImageHelpers.ConvertToGray(template) : null;
        var matchSource = graySource ?? source;
        var matchTemplate = grayTemplate ?? template;
        profile.Step("PrepareForMatching", options.UseGrayscale ? "灰度" : "直接（无 Clone）");

        var resultCols = matchSource.Width - matchTemplate.Width + 1;
        var resultRows = matchSource.Height - matchTemplate.Height + 1;
        var resultArea = (long)resultCols * resultRows;
        var scale = ComputeCoarseScale(resultArea, matchTemplate.Width, matchTemplate.Height);
        var usedPyramid = scale < 1.0;

        Mat workSource;
        Mat workTemplate;
        if (usedPyramid)
        {
            workSource = new Mat();
            Cv2.Resize(matchSource, workSource, new Size(0, 0), scale, scale, InterpolationFlags.Linear);
            workTemplate = new Mat();
            Cv2.Resize(matchTemplate, workTemplate, new Size(0, 0), scale, scale, InterpolationFlags.Linear);
            profile.Step("Coarse.Resize", $"×{scale:F3} → {workSource.Width}×{workSource.Height}, 模板 {workTemplate.Width}×{workTemplate.Height}");
        }
        else
        {
            workSource = matchSource;
            workTemplate = matchTemplate;
            profile.Step("Coarse.Resize", "未启用");
        }

        try
        {
            using var coarseResult = new Mat();
            Cv2.MatchTemplate(workSource, workTemplate, coarseResult, options.Method);
            profile.Step("Coarse.MatchTemplate", $"{coarseResult.Cols}×{coarseResult.Rows}" + (usedPyramid ? " (缩放)" : ""));

            Cv2.MinMaxLoc(coarseResult, out var minValue, out var maxValue, out var minLocation, out var maxLocation);
            profile.Step("Coarse.MinMaxLoc");

            var coarseBestLocation = options.HigherIsBetter ? maxLocation : minLocation;
            var coarseBestScore = options.HigherIsBetter ? maxValue : 1.0 - minValue;
            var coarseCandidates = SelectCoarseCandidates(coarseResult, options, coarseBestLocation, coarseBestScore);
            profile.Step("Coarse.SelectCandidates", $"候选 {coarseCandidates.Count} / 上限 {MaxCoarseCandidates}");

            var fineInputs = BuildFineInputs(coarseCandidates, matchTemplate.Size(), matchSource.Size(), scale, options.NmsOverlapThreshold);
            profile.Step("Coarse.NMS", $"精修候选 {fineInputs.Count} / 上限 {MaxFineCandidates}");

            if (fineInputs.Count == 0)
                throw new InvalidOperationException("粗匹配阶段没有生成可精修候选。");

            var canRefineAtFullResolution = (long)matchTemplate.Width * matchTemplate.Height <= MaxFullResolutionRefineTemplatePixels;
            MatchCandidate bestCandidate;
            var refinedCandidates = canRefineAtFullResolution
                ? RefineCandidates(matchSource, matchTemplate, fineInputs, options, scale, profile, out bestCandidate)
                : UseCoarseCandidates(fineInputs, options, matchTemplate.Size(), profile, out bestCandidate);

            var matches = MatchCandidateUtilities.ApplyNonMaximumSuppression(refinedCandidates, options.NmsOverlapThreshold);
            profile.Step("NMS", $"匹配 {matches.Count}");

            stopwatch.Stop();
            var profileResult = profile.Finish();

            return new TemplateLocatorResult(
                bestCandidate.Rect.Location,
                bestCandidate.Score,
                template.Size(),
                refinedCandidates,
                matches,
                stopwatch.Elapsed,
                profileResult,
                scale,
                matchSource.Size(),
                matchTemplate.Size(),
                workSource.Size(),
                workTemplate.Size(),
                coarseResult.Size(),
                CoarseResultTargetPixels,
                MinCoarseTemplateEdge);
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

    private static double ComputeCoarseScale(long resultArea, int templateWidth, int templateHeight)
    {
        if (resultArea <= CoarseResultTargetPixels)
            return 1.0;

        var scale = Math.Sqrt((double)CoarseResultTargetPixels / resultArea);
        var minDim = Math.Min(templateWidth, templateHeight);
        var minEdgeScale = (double)MinCoarseTemplateEdge / minDim;
        if (minDim * scale < MinCoarseTemplateEdge)
            scale = minEdgeScale;

        return Math.Min(scale, 1.0);
    }

    private static unsafe List<CoarseCandidate> SelectCoarseCandidates(
        Mat result,
        TemplateLocatorOptions options,
        Point bestLocation,
        double bestScore)
    {
        var threshold = Math.Max(0.0, options.Threshold);
        var top = new PriorityQueue<CoarseCandidate, double>();
        var ptr = (float*)result.DataPointer;
        var step = (int)(result.Step() / sizeof(float));

        for (var y = 0; y < result.Rows; y++)
        {
            var row = ptr + y * step;
            for (var x = 0; x < result.Cols; x++)
            {
                var raw = row[x];
                var score = options.HigherIsBetter ? raw : 1.0 - raw;
                if (double.IsNaN(score) || score < threshold)
                    continue;

                AddTopCandidate(top, new CoarseCandidate(new Point(x, y), score));
            }
        }

        var candidates = new List<CoarseCandidate>(top.Count + 1);
        while (top.TryDequeue(out var candidate, out _))
            candidates.Add(candidate);

        AddRequiredCandidate(candidates, new CoarseCandidate(bestLocation, bestScore));
        candidates.Sort(CompareCoarseCandidateScore);
        return candidates;
    }

    private static void AddTopCandidate(
        PriorityQueue<CoarseCandidate, double> top,
        CoarseCandidate candidate)
    {
        if (top.Count < MaxCoarseCandidates)
        {
            top.Enqueue(candidate, candidate.Score);
            return;
        }

        top.TryPeek(out _, out var worstScore);
        if (candidate.Score <= worstScore)
            return;

        top.Dequeue();
        top.Enqueue(candidate, candidate.Score);
    }

    private static void AddRequiredCandidate(List<CoarseCandidate> top, CoarseCandidate candidate)
    {
        for (var i = 0; i < top.Count; i++)
        {
            if (top[i].Location == candidate.Location)
                return;
        }

        if (top.Count < MaxCoarseCandidates)
        {
            top.Add(candidate);
            return;
        }

        var worstIndex = 0;
        var worstScore = top[0].Score;
        for (var i = 1; i < top.Count; i++)
        {
            if (top[i].Score >= worstScore)
                continue;

            worstScore = top[i].Score;
            worstIndex = i;
        }
        top[worstIndex] = candidate;
    }

    private static List<MatchCandidate> BuildFineInputs(
        List<CoarseCandidate> coarseCandidates,
        Size templateSize,
        Size sourceSize,
        double scale,
        double nmsOverlapThreshold)
    {
        var maxX = sourceSize.Width - templateSize.Width;
        var maxY = sourceSize.Height - templateSize.Height;
        var coarseMatches = new List<MatchCandidate>(coarseCandidates.Count);
        for (var i = 0; i < coarseCandidates.Count; i++)
        {
            var coarse = coarseCandidates[i];
            var x = MapCoordinate(coarse.Location.X, scale, maxX);
            var y = MapCoordinate(coarse.Location.Y, scale, maxY);
            coarseMatches.Add(new MatchCandidate(new Rect(x, y, templateSize.Width, templateSize.Height), coarse.Score));
        }

        var fineInputs = MatchCandidateUtilities.ApplyNonMaximumSuppression(coarseMatches, nmsOverlapThreshold);
        if (fineInputs.Count <= MaxFineCandidates)
            return fineInputs;

        return fineInputs.GetRange(0, MaxFineCandidates);
    }

    private static List<MatchCandidate> RefineCandidates(
        Mat source,
        Mat template,
        List<MatchCandidate> fineInputs,
        TemplateLocatorOptions options,
        double scale,
        PerformanceProfile profile,
        out MatchCandidate bestCandidate)
    {
        var refinedCandidates = new List<MatchCandidate>(fineInputs.Count);
        bestCandidate = default;
        var hasBest = false;
        var refineRadius = ComputeRefineRadius(scale);
        for (var i = 0; i < fineInputs.Count; i++)
        {
            var refined = RefineCandidate(source, template, fineInputs[i].Rect.Location, refineRadius, options);
            if (!hasBest || refined.Score > bestCandidate.Score)
            {
                bestCandidate = refined;
                hasBest = true;
            }

            if (refined.Score >= options.Threshold)
                refinedCandidates.Add(refined);
        }
        profile.Step("Fine.Refine", $"候选 {fineInputs.Count}, 半径 {refineRadius}px, 命中 {refinedCandidates.Count}");

        if (!hasBest)
            throw new InvalidOperationException("精修阶段没有生成匹配结果。");

        return refinedCandidates;
    }

    private static List<MatchCandidate> UseCoarseCandidates(
        List<MatchCandidate> fineInputs,
        TemplateLocatorOptions options,
        Size templateSize,
        PerformanceProfile profile,
        out MatchCandidate bestCandidate)
    {
        bestCandidate = fineInputs[0];
        var refinedCandidates = new List<MatchCandidate>(fineInputs.Count);
        for (var i = 0; i < fineInputs.Count; i++)
        {
            var input = fineInputs[i];
            var candidate = new MatchCandidate(
                new Rect(input.Rect.X, input.Rect.Y, templateSize.Width, templateSize.Height),
                input.Score);
            if (candidate.Score > bestCandidate.Score)
                bestCandidate = candidate;

            if (candidate.Score >= options.Threshold)
                refinedCandidates.Add(candidate);
        }

        profile.Step("Fine.Refine", $"跳过：模板超过 {MaxFullResolutionRefineTemplatePixels} px, 命中 {refinedCandidates.Count}");
        return refinedCandidates;
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

    private static int MapCoordinate(int value, double scale, int maxValue)
    {
        var mapped = scale < 1.0
            ? (int)Math.Round(value / scale)
            : value;
        return Math.Clamp(mapped, 0, maxValue);
    }

    private static int ComputeRefineRadius(double scale)
    {
        if (scale >= 1.0)
            return MinRefineRadiusPixels;

        var radius = (int)Math.Ceiling(RefineRadiusCoarsePixels / scale);
        return Math.Max(radius, MinRefineRadiusPixels);
    }

    private static int CompareCoarseCandidateScore(CoarseCandidate a, CoarseCandidate b)
    {
        if (b.Score > a.Score) return 1;
        if (b.Score < a.Score) return -1;
        return 0;
    }

    private readonly record struct CoarseCandidate(Point Location, double Score);
}
