using System;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Profiling;

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
