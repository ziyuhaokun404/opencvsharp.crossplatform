using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Services;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public sealed partial class BenchmarkPanelViewModel : ObservableObject
{
    private const int DefaultRunCount = 100;
    private readonly ITemplateMatchHost host;
    private readonly TemplateMatchOrchestrator orchestrator;
    private readonly PerformanceLogger logger;

    public BenchmarkPanelViewModel(
        ITemplateMatchHost host,
        TemplateMatchOrchestrator orchestrator,
        PerformanceLogger logger)
    {
        this.host = host;
        this.orchestrator = orchestrator;
        this.logger = logger;
    }

    public bool HasSummary => Summary is not null;

    public bool HasDetailedResult => DetailedResult is not null;

    public string StabilityButtonText => $"稳定性{StabilityRunCount}次";

    public string DetailedButtonText => $"性能分析{DetailedRunCount}次";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StabilityButtonText))]
    private int stabilityRunCount = DefaultRunCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailedButtonText))]
    private int detailedRunCount = DefaultRunCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private BenchmarkSummary? summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetailedResult))]
    private DetailedBenchmarkResult? detailedResult;

    [ObservableProperty]
    private string resultText = "";

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private bool isRunning;

    partial void OnIsRunningChanged(bool value)
    {
        RunStabilityBenchmarkCommand.NotifyCanExecuteChanged();
        RunDetailedBenchmarkCommand.NotifyCanExecuteChanged();
    }

    public void Clear()
    {
        ResultText = "";
        Summary = null;
        DetailedResult = null;
        Visible = false;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunStabilityBenchmarkAsync()
    {
        if (!orchestrator.HasPair)
        {
            host.SetStatus("请先导入源图和模板图。");
            return;
        }

        if (!ValidateRunCount("稳定性测试", StabilityRunCount, out var runCount))
            return;

        host.IsBusy = true;
        IsRunning = true;
        Visible = true;
        ResultText = $"正在运行稳定性测试：0/{runCount}";
        host.SetStatus($"正在运行 {runCount} 次稳定性测试...");

        try
        {
            var algorithm = TemplateLocatorSnapshotFactory.CreateSnapshot(host.SelectedLocator);
            var options = host.CreateMatchOptions();
            var result = await orchestrator.RunStabilityBenchmarkAsync(algorithm, options, runCount);

            ResultText = result.ToDisplayText(runCount);
            Summary = result;
            Visible = true;
            host.SetStatus($"稳定性测试完成：平均 {result.AverageMs:0.000} ms，P95 {result.P95Ms:0.000} ms，标准差 {result.StandardDeviationMs:0.000} ms。");
            logger.Info($"Benchmark completed. Runs={runCount}, Avg={result.AverageMs:F3}ms, Min={result.MinMs:F3}ms, Max={result.MaxMs:F3}ms, P50={result.P50Ms:F3}ms, P95={result.P95Ms:F3}ms, StdDev={result.StandardDeviationMs:F3}ms, Stable={result.IsMatchStable}");
        }
        catch (Exception ex)
        {
            Visible = true;
            ResultText = "稳定性测试失败，请检查源图、模板图和参数。";
            Summary = null;
            host.SetStatus(ResultText);
            logger.Error("RunBenchmark failed", ex);
        }
        finally
        {
            IsRunning = false;
            host.IsBusy = false;
            RunStabilityBenchmarkCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunDetailedBenchmarkAsync()
    {
        if (!orchestrator.HasPair)
        {
            host.SetStatus("请先导入源图和模板图。");
            return;
        }

        if (!ValidateRunCount("性能分析", DetailedRunCount, out var runCount))
            return;

        host.IsBusy = true;
        IsRunning = true;
        host.SetStatus($"正在运行性能分析（{runCount} 次）...");

        try
        {
            var algorithm = TemplateLocatorSnapshotFactory.CreateSnapshot(host.SelectedLocator);
            var options = host.CreateMatchOptions();
            var result = await orchestrator.RunDetailedBenchmarkAsync(algorithm, options, runCount);

            DetailedResult = result;
            host.SetStatus($"性能分析完成：平均 {result.TimingSummary.AverageMs:0.000} ms，每次分配 {result.PerCallAllocatedBytes / 1024.0:0.0} KB，Gen0 GC {result.Gen0Collections} 次。");
            logger.Info($"DetailedBenchmark completed. Runs={runCount}, Avg={result.TimingSummary.AverageMs:F3}ms, PerCallAlloc={result.PerCallAllocatedBytes}B, Gen0={result.Gen0Collections}, Gen1={result.Gen1Collections}");
        }
        catch (Exception ex)
        {
            DetailedResult = null;
            host.SetStatus("性能分析失败，请检查源图、模板图和参数。");
            logger.Error("RunDetailedBenchmark failed", ex);
        }
        finally
        {
            IsRunning = false;
            host.IsBusy = false;
        }
    }

    private bool CanRun() => !host.IsBusy && !IsRunning;

    public void NotifyHostBusyChanged()
    {
        RunStabilityBenchmarkCommand.NotifyCanExecuteChanged();
        RunDetailedBenchmarkCommand.NotifyCanExecuteChanged();
    }

    private bool ValidateRunCount(string title, int runCount, out int validatedRunCount)
    {
        validatedRunCount = runCount;
        if (runCount > 0)
            return true;

        host.SetStatus($"{title}次数必须大于 0。");
        return false;
    }
}
