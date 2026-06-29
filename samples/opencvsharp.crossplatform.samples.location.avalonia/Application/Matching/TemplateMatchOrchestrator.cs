using System.Threading.Tasks;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Benchmark;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Services;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;

/// <summary>
/// Coordinates template match, train, and benchmark execution away from the ViewModel layer.
/// </summary>
public sealed class TemplateMatchOrchestrator
{
    private readonly TemplateImageSession imageSession;
    private readonly PerformanceLogger logger;

    public TemplateMatchOrchestrator(TemplateImageSession imageSession, PerformanceLogger logger)
    {
        this.imageSession = imageSession;
        this.logger = logger;
    }

    public bool HasPair => imageSession.HasPair;

    public (byte[] Source, byte[] Template) CloneImageBytes() => imageSession.CloneImageBytes();

    public async Task<MatchOrchestrationResult> RunMatchAsync(
        ITemplateLocator locator,
        ITemplateLocator activeLocator,
        TemplateLocatorOptions options)
    {
        if (!imageSession.HasPair)
            return MatchOrchestrationResult.NoPair;

        try
        {
            var (sourceBytes, templateBytes) = imageSession.CloneImageBytes();
            var result = await Task.Run(() =>
            {
                using var source = OpenCvImageCodec.Decode(sourceBytes);
                using var template = OpenCvImageCodec.Decode(templateBytes);
                return TemplateMatchRunner.Locate(locator, source, template, options);
            }).ConfigureAwait(false);

            return MatchOrchestrationResult.Succeeded(result);
        }
        catch (System.InvalidOperationException ex) when (ex.Message is "模板图必须小于源图。" or "模板尺寸不能大于源图像。")
        {
            return MatchOrchestrationResult.TemplateTooLarge(ex.Message);
        }
        catch (System.Exception ex)
        {
            logger.Error("RunMatch failed", ex);
            return MatchOrchestrationResult.Failed;
        }
        finally
        {
            TemplateLocatorSnapshotFactory.DisposeOwnedLocator(locator, activeLocator);
        }
    }

    public async Task<TrainOrchestrationResult> TrainContourAsync(
        ContourExtractionSettings settings,
        double nmsOverlapThreshold)
    {
        if (!imageSession.HasPair)
            return TrainOrchestrationResult.Failed("请先导入源图和模板图。");

        try
        {
            var (sourceBytes, templateBytes) = imageSession.CloneImageBytes();
            var result = await Task.Run(() =>
            {
                var trainLocator = new ContourTemplateLocator();
                trainLocator.ApplySettings(settings);
                return TemplateMatchRunner.Train(trainLocator, sourceBytes, templateBytes, nmsOverlapThreshold);
            }).ConfigureAwait(false);

            return TrainOrchestrationResult.Succeeded(result);
        }
        catch (System.Exception ex)
        {
            logger.Error("TrainTemplate failed", ex);
            return TrainOrchestrationResult.Failed("模板训练失败，请调整选区或更换模板图。");
        }
    }

    public async Task<BenchmarkSummary> RunStabilityBenchmarkAsync(
        ITemplateLocator locator,
        TemplateLocatorOptions options,
        int runCount)
    {
        var (sourceBytes, templateBytes) = imageSession.CloneImageBytes();
        return await Task.Run(() =>
            StabilityBenchmarkRunner.Run(locator, sourceBytes, templateBytes, options, runCount)).ConfigureAwait(false);
    }

    public async Task<DetailedBenchmarkResult> RunDetailedBenchmarkAsync(
        ITemplateLocator locator,
        TemplateLocatorOptions options,
        int runCount,
        int warmupRuns = 5)
    {
        var (sourceBytes, templateBytes) = imageSession.CloneImageBytes();
        return await Task.Run(() =>
        {
            using var source = OpenCvImageCodec.Decode(sourceBytes);
            using var template = OpenCvImageCodec.Decode(templateBytes);
            return DetailedBenchmarkRunner.Run(locator, source, template, options, warmupRuns, runCount);
        }).ConfigureAwait(false);
    }
}
