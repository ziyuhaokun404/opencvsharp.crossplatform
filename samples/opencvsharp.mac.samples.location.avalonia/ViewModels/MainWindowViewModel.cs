using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.Mac.Core;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;
using OpenCvSharp.Mac.Samples.Location.Avalonia.Services;
using OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models;
using OpenCvSharp.Mac.Samples.Location.Avalonia.Views;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int DefaultBenchmarkRunCount = 100;
    private readonly WindowImageFileDialogService fileDialogService;
    private readonly TemplateMatchSettingsStore settingsStore = new();
    private readonly ContourTemplateLocator contourLocator = new();
    private readonly PerformanceLogger logger = new();
    private byte[] sourceBytes = [];
    private byte[] templateBytes = [];
    private Mat? cachedSourceMat;
    private Mat? cachedTemplateMat;
    private bool isSyncingThreshold;
    private bool hasRunMatch;
    private bool disposed;

    [ObservableProperty]
    private double nmsOverlapThreshold = 0.3;

    [ObservableProperty]
    private double minContourArea = 20;

    [ObservableProperty]
    private double cannyLowThreshold = 24;

    [ObservableProperty]
    private double cannyHighThreshold = 72;

    [ObservableProperty]
    private double gradientThresholdScale = 1.0;

    [ObservableProperty]
    private double closeIterations = 1;

    [ObservableProperty]
    private double maxTemplateContours = 16;

    [ObservableProperty]
    private double minimumTemplateContourCoverage = 0.5;

    [ObservableProperty]
    private double edgeDistanceTolerance = 8;

    [ObservableProperty]
    private Bitmap? sourceImageSource;

    [ObservableProperty]
    private Bitmap? templateImageSource;

    [ObservableProperty]
    private TemplateLocatorViewModel selectedAlgorithm;

    [ObservableProperty]
    private TemplateMatchMethodViewModel selectedMethod;

    [ObservableProperty]
    private bool useGrayscale = true;

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private bool isBusy;

    public bool IsReady => !IsBusy;

    [ObservableProperty]
    private string bestScoreText = "-";

    [ObservableProperty]
    private string bestPointText = "-";

    [ObservableProperty]
    private string matchCountText = "-";

    [ObservableProperty]
    private string sourceSizeText = "";

    [ObservableProperty]
    private bool matchCountBadgeVisible;

    [ObservableProperty]
    private string profileDisplayText = "";

    [ObservableProperty]
    private IReadOnlyList<ProfileStepViewModel> profileSteps = [];

    [ObservableProperty]
    private string profileTotalText = "";

    [ObservableProperty]
    private bool profileVisible;

    [ObservableProperty]
    private string pyramidInfoText = "";

    [ObservableProperty]
    private bool pyramidInfoVisible;

    [ObservableProperty]
    private string pyramidModeText = "-";

    [ObservableProperty]
    private string pyramidScaleText = "-";

    [ObservableProperty]
    private string pyramidSourceText = "-";

    [ObservableProperty]
    private string pyramidTemplateText = "-";

    [ObservableProperty]
    private string pyramidResultText = "-";

    [ObservableProperty]
    private string pyramidThresholdText = "-";

    [ObservableProperty]
    private string pyramidMinTemplateEdgeText = "-";

    [ObservableProperty]
    private string benchmarkResultText = "";

    [ObservableProperty]
    private bool benchmarkVisible;

    [ObservableProperty]
    private bool isBenchmarking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StabilityBenchmarkButtonText))]
    private int stabilityBenchmarkRunCount = DefaultBenchmarkRunCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailedBenchmarkButtonText))]
    private int detailedBenchmarkRunCount = DefaultBenchmarkRunCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetailedBenchmarkResult))]
    private DetailedBenchmarkResult? detailedBenchmarkResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBenchmarkSummary))]
    private BenchmarkSummary? benchmarkSummary;

    [ObservableProperty]
    private IReadOnlyList<MatchOverlayViewModel> matchOverlays = [];

    private double threshold = 0.65;
    private string thresholdText = "0.65";

    private bool isUpdatingSettings;

    private void UpdateContourSettings()
    {
        if (isUpdatingSettings)
            return;

        var settings = new ContourExtractionSettings(
            MinContourArea,
            CannyLowThreshold,
            CannyHighThreshold,
            GradientThresholdScale,
            (int)CloseIterations,
            (int)MaxTemplateContours,
            MinimumTemplateContourCoverage,
            EdgeDistanceTolerance
        );
        contourLocator.ApplySettings(settings);
        RefreshTemplatePreview();
        RunMatchIfActive();
    }

    private void SetContourSettingsToViewModel(ContourExtractionSettings settings)
    {
        isUpdatingSettings = true;
        MinContourArea = settings.MinimumContourArea;
        CannyLowThreshold = settings.CannyLowThreshold;
        CannyHighThreshold = settings.CannyHighThreshold;
        GradientThresholdScale = settings.GradientThresholdScale;
        CloseIterations = settings.CloseIterations;
        MaxTemplateContours = settings.MaximumTemplateContours;
        MinimumTemplateContourCoverage = settings.MinimumTemplateContourCoverage;
        EdgeDistanceTolerance = settings.EdgeDistanceTolerance;
        isUpdatingSettings = false;

        contourLocator.ApplySettings(settings);
        RefreshTemplatePreview();
    }

    partial void OnNmsOverlapThresholdChanged(double value) => RunMatchIfActive();
    partial void OnMinContourAreaChanged(double value) => UpdateContourSettings();
    partial void OnCannyLowThresholdChanged(double value) => UpdateContourSettings();
    partial void OnCannyHighThresholdChanged(double value) => UpdateContourSettings();
    partial void OnGradientThresholdScaleChanged(double value) => UpdateContourSettings();
    partial void OnCloseIterationsChanged(double value) => UpdateContourSettings();
    partial void OnMaxTemplateContoursChanged(double value) => UpdateContourSettings();
    partial void OnMinimumTemplateContourCoverageChanged(double value) => UpdateContourSettings();
    partial void OnEdgeDistanceToleranceChanged(double value) => UpdateContourSettings();

    public MainWindowViewModel(WindowImageFileDialogService fileDialogService)
    {
        this.fileDialogService = fileDialogService;
        Algorithms =
        [
            new("模板匹配", new MatchTemplateLocator(), true),
            new("粗到细模板匹配", new CoarseToFineMatchTemplateLocator(), true),
            new("轮廓匹配", contourLocator, false)
        ];
        Methods =
        [
            new("归一化相关系数", TemplateMatchModes.CCoeffNormed, true),
            new("归一化互相关", TemplateMatchModes.CCorrNormed, true),
            new("归一化平方差", TemplateMatchModes.SqDiffNormed, false)
        ];
        selectedAlgorithm = Algorithms[0];
        selectedMethod = Methods[0];
        if (!LoadPersistedImages())
            SetContourSettingsToViewModel(contourLocator.CurrentSettings);
        logger.Info($"ViewModel initialized. Algorithm={selectedAlgorithm.Name}, Method={selectedMethod.Name}, Threshold={threshold:F2}, UseGrayscale={useGrayscale}");
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ReplaceSourceImage(null);
        ReplaceTemplateImage(null);
        InvalidateImageCache();
        logger.Dispose();
    }

    public ObservableCollection<TemplateLocatorViewModel> Algorithms { get; }

    public ObservableCollection<TemplateMatchMethodViewModel> Methods { get; }

    public bool IsTemplateMatchMethodEnabled => SelectedAlgorithm.UsesTemplateMatchMethod;

    public bool IsContourTrainingEnabled => SelectedAlgorithm.Locator is ContourTemplateLocator;

    public bool HasBenchmarkSummary => BenchmarkSummary is not null;

    public bool HasDetailedBenchmarkResult => DetailedBenchmarkResult is not null;

    public string StabilityBenchmarkButtonText => $"稳定性{StabilityBenchmarkRunCount}次";

    public string DetailedBenchmarkButtonText => $"性能分析{DetailedBenchmarkRunCount}次";

    public int SourcePixelWidth { get; private set; }

    public int SourcePixelHeight { get; private set; }

    public double Threshold
    {
        get => threshold;
        set
        {
            var normalized = Math.Clamp(value, 0, 1);
            if (!SetProperty(ref threshold, normalized))
                return;

            isSyncingThreshold = true;
            ThresholdText = normalized.ToString("0.00");
            isSyncingThreshold = false;
            RunMatchIfActive();
        }
    }

    public string ThresholdText
    {
        get => thresholdText;
        set
        {
            if (!SetProperty(ref thresholdText, value) || isSyncingThreshold)
                return;

            if (double.TryParse(value, out var parsed))
                Threshold = parsed;
        }
    }

    partial void OnSelectedAlgorithmChanged(TemplateLocatorViewModel value)
    {
        OnPropertyChanged(nameof(IsTemplateMatchMethodEnabled));
        OnPropertyChanged(nameof(IsContourTrainingEnabled));
        RefreshTemplatePreview();
        logger.Info($"Algorithm changed to: {value.Name}");
        RunMatchIfActive();
    }

    partial void OnSelectedMethodChanged(TemplateMatchMethodViewModel value)
    {
        logger.Info($"Method changed to: {value.Name} ({value.Mode})");
        RunMatchIfActive();
    }

    partial void OnUseGrayscaleChanged(bool value)
    {
        logger.Info($"UseGrayscale changed to: {value}");
        RunMatchIfActive();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReady));
        RunBenchmarkCommand.NotifyCanExecuteChanged();
        RunDetailedBenchmarkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ImportSourceAsync()
    {
        var image = await fileDialogService.OpenImageAsync("导入源图");
        if (image is null)
            return;

        sourceBytes = image.Bytes;
        InvalidateSourceCache();
        ReplaceSourceImage(CreateBitmap(sourceBytes));
        UpdateSourceSize(sourceBytes);
        SaveCurrentImages();
        ClearMatchResult();
        logger.Info($"Source imported. Size={SourcePixelWidth}×{SourcePixelHeight}, Bytes={sourceBytes.Length}");
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        var image = await fileDialogService.OpenImageAsync("导入模板图");
        if (image is null)
            return;

        templateBytes = image.Bytes;
        InvalidateTemplateCache();
        RefreshTemplatePreview();
        SaveCurrentImages();
        ClearMatchResult();
        logger.Info($"Template imported. Bytes={templateBytes.Length}");
    }

    public void SetTemplateFromSourceRotatedRoi(RotatedRect roi)
    {
        if (sourceBytes.Length == 0)
            return;

        using var source = Decode(sourceBytes);
        if (roi.Size.Width < 4 || roi.Size.Height < 4)
            return;

        using var rotation = Cv2.GetRotationMatrix2D(roi.Center, -roi.Angle, 1);
        using var rotated = new Mat();
        Cv2.WarpAffine(source, rotated, rotation, source.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

        var x = (int)Math.Round(roi.Center.X - roi.Size.Width / 2);
        var y = (int)Math.Round(roi.Center.Y - roi.Size.Height / 2);
        var width = (int)Math.Round(roi.Size.Width);
        var height = (int)Math.Round(roi.Size.Height);
        var clamped = new Rect(x, y, width, height).Intersect(new Rect(0, 0, rotated.Width, rotated.Height));
        if (clamped.Width < 4 || clamped.Height < 4)
            return;

        using var cropped = new Mat(rotated, new CvRect(clamped.X, clamped.Y, clamped.Width, clamped.Height));
        templateBytes = EncodePng(cropped);
        InvalidateTemplateCache();
        RefreshTemplatePreview();
        SaveCurrentImages();
        StatusText = $"已从选区生成模板：横坐标={clamped.X}，纵坐标={clamped.Y}，宽={clamped.Width}，高={clamped.Height}。";
        ClearMatchResult();
        logger.Info($"Template from ROI. Rect=({clamped.X},{clamped.Y},{clamped.Width},{clamped.Height}), Angle={roi.Angle:F1}");
    }

    [RelayCommand]
    private async Task TrainTemplateAsync()
    {
        if (IsBusy)
            return;

        if (sourceBytes.Length == 0 || templateBytes.Length == 0)
        {
            StatusText = "请先导入源图和模板图。";
            return;
        }

        if (SelectedAlgorithm.Locator is not ContourTemplateLocator locator)
        {
            StatusText = "当前算法不需要训练模板。";
            return;
        }

        IsBusy = true;
        StatusText = "正在训练模板，请稍候...";
        var shouldRefreshMatch = false;

        try
        {
            var sourceImageBytes = sourceBytes.ToArray();
            var templateImageBytes = templateBytes.ToArray();
            var result = await Task.Run(() =>
            {
                using var source = Decode(sourceImageBytes);
                using var template = Decode(templateImageBytes);
                return locator.Train(source, template, NmsOverlapThreshold);
            });
            SetContourSettingsToViewModel(result.Settings);
            Threshold = result.SuggestedThreshold;
            SaveCurrentImages();
            StatusText = $"模板训练完成。轮廓 {result.TemplateContourCount}，候选 {result.CandidateCount}，匹配 {result.MatchCount}，建议阈值 {result.SuggestedThreshold:0.00}，最佳分数 {result.BestScore:0.0000}，Canny {result.Settings.CannyLowThreshold:0}/{result.Settings.CannyHighThreshold:0}，梯度 {result.Settings.GradientThresholdScale:0.00}，耗时 {result.Elapsed.TotalMilliseconds:0.0} 毫秒。";
            logger.Info($"Train completed. Contours={result.TemplateContourCount}, Candidates={result.CandidateCount}, Matches={result.MatchCount}, BestScore={result.BestScore:F4}, SuggestedThreshold={result.SuggestedThreshold:F2}, Canny={result.Settings.CannyLowThreshold:F0}/{result.Settings.CannyHighThreshold:F0}, Gradient={result.Settings.GradientThresholdScale:F2}, Elapsed={result.Elapsed.TotalMilliseconds:F1}ms");
            shouldRefreshMatch = hasRunMatch;
        }
        catch (Exception ex)
        {
            ClearMatchResult();
            StatusText = "模板训练失败，请调整选区或更换模板图。";
            logger.Error("TrainTemplate failed", ex);
        }
        finally
        {
            IsBusy = false;
        }

        if (shouldRefreshMatch)
            RunMatch();
    }

    [RelayCommand]
    private void RunMatch()
    {
        if (IsBusy)
            return;

        hasRunMatch = true;
        if (sourceBytes.Length == 0 || templateBytes.Length == 0)
        {
            ClearMatchResult();
            return;
        }

        try
        {
            var source = GetCachedSourceMat();
            var template = GetCachedTemplateMat();

            if (template.Width > source.Width || template.Height > source.Height)
            {
                ClearMatchResult();
                StatusText = "模板图必须小于源图。";
                return;
            }

            var result = SelectedAlgorithm.Locator.Locate(
                source,
                template,
                new TemplateLocatorOptions(
                    SelectedMethod.Mode,
                    SelectedMethod.HigherIsBetter,
                    Threshold,
                    UseGrayscale,
                    NmsOverlapThreshold));

            MatchOverlays = CreateMatchOverlays(result);
            BestScoreText = result.BestScore.ToString("0.0000");
            BestPointText = $"({result.BestLocation.X}, {result.BestLocation.Y})";
            MatchCountText = result.Matches.Count.ToString();
            MatchCountBadgeVisible = result.Matches.Count > 0;

            if (result.Profile is not null)
            {
                ProfileDisplayText = result.Profile.ToDisplayText();
                ProfileSteps = result.Profile.ToStepViewModels();
                ProfileTotalText = $"总耗时 {result.Profile.TotalMs:0.000} ms";
                ProfileVisible = true;
                StatusText = result.Profile.ToStatusText();
                logger.LogProfile(result.Profile);
            }
            else
            {
                var methodText = SelectedAlgorithm.UsesTemplateMatchMethod ? $"，方法：{SelectedMethod.Name}" : "";
                StatusText = $"{SelectedAlgorithm.Locator.Name}{methodText}。耗时 {result.Elapsed.TotalMilliseconds:0.0} 毫秒。";
            }

            UpdatePyramidInfo(result);
        }
        catch (Exception ex)
        {
            ClearMatchResult();
            StatusText = "模板匹配失败，请检查源图、模板图和参数。";
            logger.Error("RunMatch failed", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunBenchmark))]
    private async Task RunBenchmarkAsync()
    {
        if (sourceBytes.Length == 0 || templateBytes.Length == 0)
        {
            StatusText = "请先导入源图和模板图。";
            return;
        }

        if (!ValidateBenchmarkRunCount("稳定性测试", StabilityBenchmarkRunCount, out var runCount))
            return;

        IsBusy = true;
        IsBenchmarking = true;
        BenchmarkVisible = true;
        BenchmarkResultText = $"正在运行稳定性测试：0/{runCount}";
        StatusText = $"正在运行 {runCount} 次稳定性测试...";

        try
        {
            var sourceImageBytes = sourceBytes.ToArray();
            var templateImageBytes = templateBytes.ToArray();
            var algorithm = CreateBenchmarkLocatorSnapshot();
            var options = new TemplateLocatorOptions(
                SelectedMethod.Mode,
                SelectedMethod.HigherIsBetter,
                Threshold,
                UseGrayscale,
                NmsOverlapThreshold);

            var result = await Task.Run(() =>
            {
                using var source = Decode(sourceImageBytes);
                using var template = Decode(templateImageBytes);
                if (template.Width > source.Width || template.Height > source.Height)
                    throw new InvalidOperationException("模板图必须小于源图。");

                algorithm.Locate(source, template, options);
                var runs = new List<TemplateLocatorResult>(runCount);
                for (var i = 0; i < runCount; i++)
                    runs.Add(algorithm.Locate(source, template, options));

                return BenchmarkSummary.Create(runs);
            });

            BenchmarkResultText = result.ToDisplayText(runCount);
            BenchmarkSummary = result;
            BenchmarkVisible = true;
            StatusText = $"稳定性测试完成：平均 {result.AverageMs:0.000} ms，P95 {result.P95Ms:0.000} ms，标准差 {result.StandardDeviationMs:0.000} ms。";
            logger.Info($"Benchmark completed. Runs={runCount}, Avg={result.AverageMs:F3}ms, Min={result.MinMs:F3}ms, Max={result.MaxMs:F3}ms, P50={result.P50Ms:F3}ms, P95={result.P95Ms:F3}ms, StdDev={result.StandardDeviationMs:F3}ms, Stable={result.IsMatchStable}");
        }
        catch (Exception ex)
        {
            BenchmarkVisible = true;
            BenchmarkResultText = "稳定性测试失败，请检查源图、模板图和参数。";
            BenchmarkSummary = null;
            StatusText = BenchmarkResultText;
            logger.Error("RunBenchmark failed", ex);
        }
        finally
        {
            IsBenchmarking = false;
            IsBusy = false;
            RunBenchmarkCommand.NotifyCanExecuteChanged();
        }
    }

    private void RunMatchIfActive()
    {
        if (hasRunMatch)
            RunMatch();
    }

    private bool CanRunBenchmark() => !IsBusy && !IsBenchmarking;

    partial void OnIsBenchmarkingChanged(bool value)
    {
        RunBenchmarkCommand.NotifyCanExecuteChanged();
        RunDetailedBenchmarkCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRunBenchmark))]
    private async Task RunDetailedBenchmarkAsync()
    {
        if (sourceBytes.Length == 0 || templateBytes.Length == 0)
        {
            StatusText = "请先导入源图和模板图。";
            return;
        }

        if (!ValidateBenchmarkRunCount("性能分析", DetailedBenchmarkRunCount, out var runCount))
            return;

        IsBusy = true;
        IsBenchmarking = true;
        StatusText = $"正在运行性能分析（{runCount} 次）...";

        try
        {
            var sourceImageBytes = sourceBytes.ToArray();
            var templateImageBytes = templateBytes.ToArray();
            var algorithm = CreateBenchmarkLocatorSnapshot();
            var options = new TemplateLocatorOptions(
                SelectedMethod.Mode,
                SelectedMethod.HigherIsBetter,
                Threshold,
                UseGrayscale,
                NmsOverlapThreshold);

            var result = await Task.Run(() =>
            {
                using var source = Decode(sourceImageBytes);
                using var template = Decode(templateImageBytes);
                if (template.Width > source.Width || template.Height > source.Height)
                    throw new InvalidOperationException("模板图必须小于源图。");

                return BenchmarkDetailWindow.RunDetailedBenchmark(
                    algorithm, source, template, options, warmupRuns: 5, benchmarkRuns: runCount);
            });

            DetailedBenchmarkResult = result;
            StatusText = $"性能分析完成：平均 {result.TimingSummary.AverageMs:0.000} ms，每次分配 {result.PerCallAllocatedBytes / 1024.0:0.0} KB，Gen0 GC {result.Gen0Collections} 次。";
            logger.Info($"DetailedBenchmark completed. Runs={runCount}, Avg={result.TimingSummary.AverageMs:F3}ms, PerCallAlloc={result.PerCallAllocatedBytes}B, Gen0={result.Gen0Collections}, Gen1={result.Gen1Collections}");
        }
        catch (Exception ex)
        {
            DetailedBenchmarkResult = null;
            StatusText = "性能分析失败，请检查源图、模板图和参数。";
            logger.Error("RunDetailedBenchmark failed", ex);
        }
        finally
        {
            IsBenchmarking = false;
            IsBusy = false;
        }
    }

    private bool ValidateBenchmarkRunCount(string title, int runCount, out int validatedRunCount)
    {
        validatedRunCount = runCount;
        if (runCount > 0)
            return true;

        StatusText = $"{title}次数必须大于 0。";
        return false;
    }

    private ITemplateLocator CreateBenchmarkLocatorSnapshot()
    {
        if (SelectedAlgorithm.Locator is not ContourTemplateLocator contour)
            return SelectedAlgorithm.Locator;

        var snapshot = new ContourTemplateLocator();
        snapshot.ApplySettings(contour.CurrentSettings);
        return snapshot;
    }

    private void ClearMatchResult()
    {
        hasRunMatch = false;
        MatchOverlays = [];
        BestScoreText = "-";
        BestPointText = "-";
        MatchCountText = "-";
        MatchCountBadgeVisible = false;
        ProfileDisplayText = "";
        ProfileSteps = [];
        ProfileTotalText = "";
        ProfileVisible = false;
        ClearPyramidInfo();
        BenchmarkResultText = "";
        BenchmarkSummary = null;
        BenchmarkVisible = false;
    }

    private void UpdatePyramidInfo(TemplateLocatorResult result)
    {
        if (result.PyramidScale is null)
        {
            PyramidInfoText = "当前算法不使用金字塔。";
            PyramidInfoVisible = true;
            PyramidModeText = "不使用";
            PyramidScaleText = "-";
            PyramidSourceText = FormatSize(result.PyramidSourceSize);
            PyramidTemplateText = FormatSize(result.PyramidTemplateSize);
            PyramidResultText = FormatSize(result.PyramidResultSize);
            PyramidThresholdText = "-";
            PyramidMinTemplateEdgeText = "-";
            return;
        }

        var source = FormatSize(result.PyramidSourceSize);
        var template = FormatSize(result.PyramidTemplateSize);
        var workSource = FormatSize(result.PyramidWorkSize);
        var workTemplate = FormatSize(result.PyramidWorkTemplateSize);
        var resultSize = FormatSize(result.PyramidResultSize);
        var threshold = result.PyramidThresholdPixels?.ToString() ?? "-";
        var minEdge = result.PyramidMinTemplateEdge?.ToString() ?? "-";

        PyramidInfoVisible = true;
        PyramidModeText = result.PyramidScale < 1.0 ? "已启用" : "未启用";
        PyramidScaleText = result.PyramidScale < 1.0 ? $"×{result.PyramidScale.Value:0.000}" : "×1.000";
        PyramidSourceText = result.PyramidScale < 1.0 ? $"{source} → {workSource}" : source;
        PyramidTemplateText = result.PyramidScale < 1.0 ? $"{template} → {workTemplate}" : template;
        PyramidResultText = resultSize;
        PyramidThresholdText = threshold;
        PyramidMinTemplateEdgeText = minEdge;
        PyramidInfoText = result.PyramidScale < 1.0
            ? $"源图和模板已缩放后匹配，结果坐标映射回原图。"
            : "当前尺寸未超过金字塔阈值，直接在原图尺度匹配。";
    }

    private void ClearPyramidInfo()
    {
        PyramidInfoText = "";
        PyramidInfoVisible = false;
        PyramidModeText = "-";
        PyramidScaleText = "-";
        PyramidSourceText = "-";
        PyramidTemplateText = "-";
        PyramidResultText = "-";
        PyramidThresholdText = "-";
        PyramidMinTemplateEdgeText = "-";
    }

    private static string FormatSize(CvSize? size)
    {
        return size is { } s ? $"{s.Width}×{s.Height}" : "-";
    }

    private bool LoadPersistedImages()
    {
        var settings = settingsStore.Load();
        if (settings is null)
            return false;

        if (settings.SourceImageBytes.Length == 0 || settings.TemplateImageBytes.Length == 0)
            throw new InvalidDataException("已保存的图像配置缺少图像数据。");

        sourceBytes = settings.SourceImageBytes;
        templateBytes = settings.TemplateImageBytes;
        InvalidateImageCache();
        if (settings.ContourSettings is not null)
            SetContourSettingsToViewModel(settings.ContourSettings);
        else
            SetContourSettingsToViewModel(contourLocator.CurrentSettings);
        ReplaceSourceImage(CreateBitmap(sourceBytes));
        RefreshTemplatePreview();
        UpdateSourceSize(sourceBytes);
        StatusText = "已加载上次保存的源图和模板图。";
        ClearMatchResult();
        return true;
    }

    private void SaveCurrentImages()
    {
        if (sourceBytes.Length == 0 || templateBytes.Length == 0)
            return;

        settingsStore.Save(new TemplateMatchSettings(sourceBytes, templateBytes, contourLocator.CurrentSettings));
    }

    private Mat GetCachedSourceMat()
    {
        cachedSourceMat ??= Decode(sourceBytes);
        return cachedSourceMat;
    }

    private Mat GetCachedTemplateMat()
    {
        cachedTemplateMat ??= Decode(templateBytes);
        return cachedTemplateMat;
    }

    private void InvalidateSourceCache()
    {
        cachedSourceMat?.Dispose();
        cachedSourceMat = null;
    }

    private void InvalidateTemplateCache()
    {
        cachedTemplateMat?.Dispose();
        cachedTemplateMat = null;
    }

    private void InvalidateImageCache()
    {
        InvalidateSourceCache();
        InvalidateTemplateCache();
    }

    private void RefreshTemplatePreview()
    {
        if (templateBytes.Length == 0)
        {
            ReplaceTemplateImage(null);
            return;
        }

        if (SelectedAlgorithm.Locator is not ContourTemplateLocator)
        {
            ReplaceTemplateImage(CreateBitmap(templateBytes));
            return;
        }

        try
        {
            using var template = Decode(templateBytes);
            using var preview = contourLocator.CreateTemplateContourPreview(template);
            ReplaceTemplateImage(CreateBitmap(EncodePng(preview)));
        }
        catch (Exception)
        {
            ReplaceTemplateImage(null);
            StatusText = "模板轮廓预览失败，请检查模板图。";
        }
    }

    private void ReplaceSourceImage(Bitmap? bitmap)
    {
        var previous = SourceImageSource;
        if (ReferenceEquals(previous, bitmap))
            return;

        SourceImageSource = bitmap;
        previous?.Dispose();
    }

    private void ReplaceTemplateImage(Bitmap? bitmap)
    {
        var previous = TemplateImageSource;
        if (ReferenceEquals(previous, bitmap))
            return;

        TemplateImageSource = bitmap;
        previous?.Dispose();
    }

    private static IReadOnlyList<MatchOverlayViewModel> CreateMatchOverlays(TemplateLocatorResult result)
    {
        if (result.Candidates.Count == 0)
            return [];

        var bestRect = new CvRect(result.BestLocation.X, result.BestLocation.Y, result.TemplateSize.Width, result.TemplateSize.Height);
        var overlays = result.Matches
            .Select(match => new MatchOverlayViewModel(
                match.Rect.X,
                match.Rect.Y,
                match.Rect.Width,
                match.Rect.Height,
                match.Rect == bestRect))
            .ToList();

        if (overlays.All(overlay => !overlay.IsBestMatch))
            overlays.Insert(0, new MatchOverlayViewModel(bestRect.X, bestRect.Y, bestRect.Width, bestRect.Height, true));

        return overlays;
    }

    private static Mat Decode(byte[] bytes)
    {
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("无法解码图像数据。");

        return mat;
    }

    private static Bitmap CreateBitmap(byte[] bytes)
    {
        return new Bitmap(new MemoryStream(bytes));
    }

    private static byte[] EncodePng(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        return bytes;
    }

    private void UpdateSourceSize(byte[] bytes)
    {
        using var source = Decode(bytes);
        SourcePixelWidth = source.Width;
        SourcePixelHeight = source.Height;
        SourceSizeText = $"{source.Width} × {source.Height}";
    }

}
