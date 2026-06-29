using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Benchmark;

internal static class DetailedBenchmarkRunner
{
    public static DetailedBenchmarkResult Run(
        ITemplateLocator locator,
        Mat source,
        Mat template,
        TemplateLocatorOptions options,
        int warmupRuns,
        int benchmarkRuns)
    {
        TemplateMatchRunner.EnsureTemplateFits(source, template);

        for (var i = 0; i < warmupRuns; i++)
            locator.Locate(source, template, options);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        var results = new List<TemplateLocatorResult>(benchmarkRuns);
        for (var i = 0; i < benchmarkRuns; i++)
            results.Add(locator.Locate(source, template, options));

        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocated = allocAfter - allocBefore;
        var summary = BenchmarkSummary.Create(results);

        return new DetailedBenchmarkResult(
            summary,
            totalAllocated,
            benchmarkRuns > 0 ? totalAllocated / benchmarkRuns : 0,
            GC.CollectionCount(0) - gen0Before,
            GC.CollectionCount(1) - gen1Before,
            GC.CollectionCount(2) - gen2Before,
            results[^1].Profile,
            results);
    }
}
