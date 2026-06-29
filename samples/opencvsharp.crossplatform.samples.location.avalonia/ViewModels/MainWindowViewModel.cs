using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Services;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;
using OpenCvSharp.CrossPlatform.Samples.Shared.Services;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable, ITemplateMatchHost
{
    private readonly IImageFileDialogService fileDialogService;
    private readonly TemplateMatchSettingsStore settingsStore = new();
    private readonly TemplateImageSession imageSession = new();
    private readonly ContourTemplateLocator contourLocator = new();
    private readonly PerformanceLogger logger = new();
    private readonly TemplateMatchOrchestrator matchOrchestrator;
    private bool hasRunMatch;
    private bool disposed;

    public MainWindowViewModel(IImageFileDialogService fileDialogService)
    {
        this.fileDialogService = fileDialogService;
        matchOrchestrator = new TemplateMatchOrchestrator(imageSession, logger);
        Options = new MatchOptionsViewModel(contourLocator);
        Contour = new ContourSettingsViewModel(contourLocator);
        Result = new MatchResultViewModel();
        Pyramid = new PyramidInfoViewModel();
        Benchmark = new BenchmarkPanelViewModel(this, matchOrchestrator, logger);

        Options.MatchSettingsChanged += (_, _) => RunMatchIfActive();
        Options.AlgorithmChanged += (_, value) =>
            logger.Info($"Algorithm changed to: {value.Name}");
        Options.AlgorithmChanged += (_, _) => RefreshTemplatePreview();
        Contour.SettingsChanged += (_, _) =>
        {
            RefreshTemplatePreview();
            RunMatchIfActive();
        };

        if (!LoadPersistedImages())
            Contour.ApplySettings(contourLocator.CurrentSettings);

        logger.Info(
            $"ViewModel initialized. Algorithm={Options.SelectedAlgorithm.Name}, Method={Options.SelectedMethod.Name}, Threshold={Options.Threshold:F2}, UseGrayscale={Options.UseGrayscale}");
    }

    public MatchOptionsViewModel Options { get; }

    public ContourSettingsViewModel Contour { get; }

    public MatchResultViewModel Result { get; }

    public PyramidInfoViewModel Pyramid { get; }

    public BenchmarkPanelViewModel Benchmark { get; }

    [ObservableProperty]
    private Bitmap? sourceImageSource;

    [ObservableProperty]
    private Bitmap? templateImageSource;

    [ObservableProperty]
    private string sourceSizeText = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private bool isBusy;

    public bool IsReady => !IsBusy;

    public int SourcePixelWidth => imageSession.SourceWidth;

    public int SourcePixelHeight => imageSession.SourceHeight;

    bool ITemplateMatchHost.HasImagePair => matchOrchestrator.HasPair;

    ITemplateLocator ITemplateMatchHost.SelectedLocator => Options.SelectedAlgorithm.Locator;

    ITemplateLocator ITemplateMatchHost.CreateMatchLocator() => Options.CreateMatchLocator();

    TemplateLocatorOptions ITemplateMatchHost.CreateMatchOptions() => Options.CreateOptions();

    bool ITemplateMatchHost.IsBusy
    {
        get => IsBusy;
        set => IsBusy = value;
    }

    (byte[] Source, byte[] Template) ITemplateMatchHost.CloneImageBytes()
        => matchOrchestrator.CloneImageBytes();

    void ITemplateMatchHost.SetStatus(string text) => StatusText = text;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ReplaceSourceImage(null);
        ReplaceTemplateImage(null);
        imageSession.Dispose();
        foreach (var algorithm in Options.Algorithms)
        {
            if (algorithm.Locator is IDisposable locator)
                locator.Dispose();
        }

        logger.Dispose();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReady));
        Benchmark.NotifyHostBusyChanged();
    }

    private void RunMatchIfActive()
    {
        if (hasRunMatch)
            _ = RunMatch();
    }

    private void ClearMatchResult()
    {
        hasRunMatch = false;
        Result.Clear();
        Pyramid.Clear();
        Benchmark.Clear();
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

    private void RefreshTemplatePreview()
    {
        try
        {
            ReplaceTemplateImage(
                AvaloniaImagePreview.CreateTemplatePreview(
                    imageSession.TemplateBytes.ToArray(),
                    Options.SelectedAlgorithm.Locator));
        }
        catch (Exception)
        {
            ReplaceTemplateImage(null);
            StatusText = "模板轮廓预览失败，请检查模板图。";
        }
    }

    private void UpdateSourceSizeDisplay()
    {
        SourceSizeText = $"{imageSession.SourceWidth} × {imageSession.SourceHeight}";
        OnPropertyChanged(nameof(SourcePixelWidth));
        OnPropertyChanged(nameof(SourcePixelHeight));
    }
}
