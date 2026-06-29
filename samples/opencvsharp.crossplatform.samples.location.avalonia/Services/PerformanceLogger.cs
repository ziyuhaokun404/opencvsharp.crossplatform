using System;
using System.IO;
using OpenCvSharp.CrossPlatform.Core.Profiling;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Profiling;
using OpenCvSharp.CrossPlatform.Samples.Shared.Logging;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Services;

/// <summary>
/// Async performance logger for template matching sessions.
/// </summary>
public sealed class PerformanceLogger : AsyncFileLogger
{
    public PerformanceLogger()
        : base(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenCvSharp.CrossPlatform.Samples.Location.Avalonia",
                "logs"),
            "template-match",
            "template-match-*.log")
    {
        Info("Session started.");
    }

    public void LogProfile(ProfileResult profile) =>
        WritePerf(profile.ToDisplayText().TrimEnd());

    protected override void OnSessionEnding() => Info("Session ended.");
}
