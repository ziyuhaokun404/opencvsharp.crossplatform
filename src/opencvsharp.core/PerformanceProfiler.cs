using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenCvSharp.Core;

/// <summary>
/// Captures sub-millisecond step timings within a single algorithm run.
/// Call <see cref="Step"/> at each phase boundary; call <see cref="Finish"/> to seal the profile.
/// The resulting <see cref="ProfileResult"/> contains every step with elapsed ticks and a formatted summary.
/// </summary>
public sealed class PerformanceProfile
{
    private readonly string operationName;
    private readonly long startTicks;
    private readonly List<ProfileStep> steps = new();
    private long lastTicks;

    public PerformanceProfile(string operationName)
    {
        this.operationName = operationName;
        startTicks = Stopwatch.GetTimestamp();
        lastTicks = startTicks;
    }

    /// <summary>
    /// Records a named step with the time elapsed since the previous step.
    /// </summary>
    public void Step(string name)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = ElapsedMs(lastTicks, now);
        steps.Add(new ProfileStep(name, elapsed));
        lastTicks = now;
    }

    /// <summary>
    /// Records a named step with an explicit sub-detail message.
    /// </summary>
    public void Step(string name, string detail)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = ElapsedMs(lastTicks, now);
        steps.Add(new ProfileStep(name, elapsed, detail));
        lastTicks = now;
    }

    /// <summary>
    /// Seals the profile and returns the complete result.
    /// </summary>
    public ProfileResult Finish()
    {
        var now = Stopwatch.GetTimestamp();
        var totalMs = ElapsedMs(startTicks, now);
        return new ProfileResult(operationName, totalMs, steps.ToArray());
    }

    private static double ElapsedMs(long from, long to)
    {
        return (to - from) * 1000.0 / Stopwatch.Frequency;
    }
}

public sealed record ProfileStep(string Name, double ElapsedMs, string? Detail = null)
{
    public override string ToString()
    {
        var ms = ElapsedMs.ToString("0.000", CultureInfo.InvariantCulture);
        return Detail is null
            ? $"  {Name,-28} {ms,8} ms"
            : $"  {Name,-28} {ms,8} ms  ({Detail})";
    }
}

public sealed record ProfileResult(string OperationName, double TotalMs, ProfileStep[] Steps)
{
    /// <summary>
    /// Multi-line formatted summary suitable for display in the UI console.
    /// </summary>
    public string ToDisplayText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"── {OperationName}  总耗时 {TotalMs:0.000} ms ──");
        foreach (var step in Steps)
            sb.AppendLine(step.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// Single-line compact summary for the status bar.
    /// </summary>
    public string ToStatusText()
    {
        var sb = new StringBuilder();
        sb.Append($"{OperationName}: ");
        for (var i = 0; i < Steps.Length; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append($"{Steps[i].Name} {Steps[i].ElapsedMs:0.00}ms");
        }
        sb.Append($" | 总计 {TotalMs:0.00}ms");
        return sb.ToString();
    }

    /// <summary>
    /// Creates view models for structured UI rendering with proportional time bars.
    /// </summary>
    public IReadOnlyList<ProfileStepViewModel> ToStepViewModels()
    {
        var result = new List<ProfileStepViewModel>(Steps.Length);
        foreach (var step in Steps)
        {
            var pct = TotalMs > 0 ? step.ElapsedMs / TotalMs * 100.0 : 0;
            result.Add(new ProfileStepViewModel(
                step.Name,
                $"{step.ElapsedMs:0.000}",
                step.Detail,
                pct));
        }
        return result;
    }
}

/// <summary>
/// View model for a single profiling step, designed for rich UI display.
/// </summary>
public sealed record ProfileStepViewModel(
    string Name,
    string TimeText,
    string? Detail,
    double Percentage)
{
    /// <summary>Width multiplier for the proportional bar (0..1).</summary>
    public double BarFraction => Math.Clamp(Percentage / 100.0, 0, 1);
}
