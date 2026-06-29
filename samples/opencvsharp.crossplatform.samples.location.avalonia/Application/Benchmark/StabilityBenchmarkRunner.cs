using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Benchmark;

internal static class StabilityBenchmarkRunner
{
    public static BenchmarkSummary Run(
        ITemplateLocator locator,
        byte[] sourceBytes,
        byte[] templateBytes,
        TemplateLocatorOptions options,
        int runCount)
    {
        using var source = OpenCvImageCodec.Decode(sourceBytes);
        using var template = OpenCvImageCodec.Decode(templateBytes);
        TemplateMatchRunner.EnsureTemplateFits(source, template);

        locator.Locate(source, template, options);
        var runs = new List<TemplateLocatorResult>(runCount);
        for (var i = 0; i < runCount; i++)
            runs.Add(locator.Locate(source, template, options));

        return BenchmarkSummary.Create(runs);
    }
}
