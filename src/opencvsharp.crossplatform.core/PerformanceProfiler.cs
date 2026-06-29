using System.Collections.Generic;
using System.Diagnostics;

namespace OpenCvSharp.CrossPlatform.Core.Profiling;

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

public sealed record ProfileStep(string Name, double ElapsedMs, string? Detail = null);

public sealed record ProfileResult(string OperationName, double TotalMs, ProfileStep[] Steps);
