using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class BenchmarkSummaryTests
{
    private static TemplateLocatorResult CreateRun(
        double timingMs,
        Point location = default,
        int matchCount = 1,
        double score = 0.9)
    {
        var matches = new List<MatchCandidate>(matchCount);
        for (var i = 0; i < matchCount; i++)
            matches.Add(new MatchCandidate(new Rect(location.X + i, location.Y, 10, 10), score - i * 0.01));

        return new TemplateLocatorResult(
            location,
            score,
            new Size(10, 10),
            matches,
            matches,
            TimeSpan.FromMilliseconds(timingMs),
            new ProfileResult("benchmark", timingMs, []));
    }

    [Fact]
    public void Create_ComputesAverageAndPercentiles()
    {
        var runs = new[]
        {
            CreateRun(10),
            CreateRun(20),
            CreateRun(30),
            CreateRun(40),
            CreateRun(50),
        };

        var summary = BenchmarkSummary.Create(runs);

        Assert.Equal(30, summary.AverageMs);
        Assert.Equal(10, summary.MinMs);
        Assert.Equal(50, summary.MaxMs);
        Assert.Equal(30, summary.P50Ms);
        Assert.Equal(48, summary.P95Ms, precision: 6);
        Assert.Equal(Math.Sqrt(200), summary.StandardDeviationMs, precision: 6);
        Assert.Equal(5, summary.RunTimingsMs.Count);
    }

    [Fact]
    public void Create_UsesProfileTotalMsWhenPresent()
    {
        var run = CreateRun(12.5);
        var summary = BenchmarkSummary.Create([run]);

        Assert.Equal(12.5, summary.AverageMs);
        Assert.Equal(12.5, summary.P50Ms);
        Assert.Equal(12.5, summary.P95Ms);
    }

    [Fact]
    public void Create_TracksLocationAndMatchCountStability()
    {
        var runs = new[]
        {
            CreateRun(10, new Point(1, 2), matchCount: 1),
            CreateRun(11, new Point(1, 2), matchCount: 1),
            CreateRun(12, new Point(5, 6), matchCount: 2),
        };

        var summary = BenchmarkSummary.Create(runs);

        Assert.False(summary.IsMatchStable);
        Assert.Equal(2, summary.StableBestLocationCount);
        Assert.Equal(2, summary.StableMatchCountCount);
        Assert.Equal(new Point(1, 2), summary.BestLocation);
        Assert.Equal(1, summary.MatchCount);
    }
}
