using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Core;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

public sealed record BenchmarkSummary(
    IReadOnlyList<double> RunTimingsMs,
    double AverageMs,
    double MinMs,
    double MaxMs,
    double P50Ms,
    double P95Ms,
    double StandardDeviationMs,
    int StableBestLocationCount,
    int StableMatchCountCount,
    Point BestLocation,
    int MatchCount,
    double FirstScore,
    double LastScore)
{
    public bool IsMatchStable => StableBestLocationCount == 1 && StableMatchCountCount == 1;

    public string StabilityText => IsMatchStable ? "稳定" : "存在波动";

    public string AverageText => $"{AverageMs:0.000} ms";

    public string P50Text => $"{P50Ms:0.000} ms";

    public string P95Text => $"{P95Ms:0.000} ms";

    public string StandardDeviationText => $"{StandardDeviationMs:0.000} ms";

    public string RangeText => $"{MinMs:0.000} - {MaxMs:0.000} ms";

    public string BestLocationText => $"({BestLocation.X}, {BestLocation.Y})";

    public string MatchCountText => MatchCount.ToString();

    public string ResultVarietyText => $"位置 {StableBestLocationCount} / 数量 {StableMatchCountCount}";

    public string ScoreDriftText => $"{FirstScore:0.0000} → {LastScore:0.0000}";

    public static BenchmarkSummary Create(IReadOnlyList<TemplateLocatorResult> runs)
    {
        if (runs.Count == 0)
            throw new InvalidOperationException("稳定性测试没有产生结果。");

        var runTimings = runs
            .Select(run => run.Profile?.TotalMs ?? run.Elapsed.TotalMilliseconds)
            .ToArray();
        var timings = runTimings
            .OrderBy(value => value)
            .ToArray();
        var average = timings.Average();
        var variance = timings.Select(value => Math.Pow(value - average, 2)).Average();
        var first = runs[0];
        var last = runs[^1];

        return new BenchmarkSummary(
            runTimings,
            average,
            timings[0],
            timings[^1],
            Percentile(timings, 0.50),
            Percentile(timings, 0.95),
            Math.Sqrt(variance),
            runs.Select(run => run.BestLocation).Distinct().Count(),
            runs.Select(run => run.Matches.Count).Distinct().Count(),
            first.BestLocation,
            first.Matches.Count,
            first.BestScore,
            last.BestScore);
    }

    public string ToDisplayText(int runCount)
    {
        var stableText = IsMatchStable ? "稳定" : "存在波动";
        return
            $"{runCount} 次运行耗时：平均 {AverageMs:0.000} ms，P50 {P50Ms:0.000} ms，P95 {P95Ms:0.000} ms，最小 {MinMs:0.000} ms，最大 {MaxMs:0.000} ms，标准差 {StandardDeviationMs:0.000} ms。\n" +
            $"结果稳定性：{stableText}。最佳位置 ({BestLocation.X}, {BestLocation.Y})，匹配数量 {MatchCount}，位置种类 {StableBestLocationCount}，数量种类 {StableMatchCountCount}，首末分数 {FirstScore:0.0000} / {LastScore:0.0000}。";
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 1)
            return sortedValues[0];

        var position = (sortedValues.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sortedValues[lower];

        var weight = position - lower;
        return sortedValues[lower] * (1.0 - weight) + sortedValues[upper] * weight;
    }
}
