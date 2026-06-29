using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public sealed partial class MatchOptionsViewModel : ObservableObject
{
    private readonly ContourTemplateLocator contourLocator;
    private double threshold = 0.65;
    private string thresholdText = "0.65";
    private bool isSyncingThreshold;

    public MatchOptionsViewModel(ContourTemplateLocator contourLocator)
    {
        this.contourLocator = contourLocator;
        Algorithms =
        [
            new("模板匹配", new MatchTemplateLocator(), true),
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
    }

    public ObservableCollection<TemplateLocatorViewModel> Algorithms { get; }

    public ObservableCollection<TemplateMatchMethodViewModel> Methods { get; }

    public event EventHandler? MatchSettingsChanged;

    public event EventHandler<TemplateLocatorViewModel>? AlgorithmChanged;

    public bool IsTemplateMatchMethodEnabled => SelectedAlgorithm.UsesTemplateMatchMethod;

    public bool IsContourTrainingEnabled => SelectedAlgorithm.Locator is ContourTemplateLocator;

    [ObservableProperty]
    private TemplateLocatorViewModel selectedAlgorithm;

    [ObservableProperty]
    private TemplateMatchMethodViewModel selectedMethod;

    [ObservableProperty]
    private bool useGrayscale = true;

    [ObservableProperty]
    private double nmsOverlapThreshold = 0.3;

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
            MatchSettingsChanged?.Invoke(this, EventArgs.Empty);
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

    public TemplateLocatorOptions CreateOptions()
        => new(
            SelectedMethod.Mode,
            SelectedMethod.HigherIsBetter,
            Threshold,
            UseGrayscale,
            NmsOverlapThreshold);

    public ITemplateLocator CreateMatchLocator()
    {
        if (SelectedAlgorithm.Locator is ContourTemplateLocator contour)
            return TemplateLocatorSnapshotFactory.CreateSnapshot(contour);

        if (SelectedAlgorithm.Locator is MatchTemplateLocator)
            return new MatchTemplateLocator();

        return SelectedAlgorithm.Locator;
    }

    public static void DisposeLocatorIfOwned(ITemplateLocator locator, ITemplateLocator activeLocator)
        => TemplateLocatorSnapshotFactory.DisposeOwnedLocator(locator, activeLocator);

    partial void OnSelectedAlgorithmChanged(TemplateLocatorViewModel value)
    {
        OnPropertyChanged(nameof(IsTemplateMatchMethodEnabled));
        OnPropertyChanged(nameof(IsContourTrainingEnabled));
        AlgorithmChanged?.Invoke(this, value);
        MatchSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedMethodChanged(TemplateMatchMethodViewModel value)
        => MatchSettingsChanged?.Invoke(this, EventArgs.Empty);

    partial void OnUseGrayscaleChanged(bool value)
        => MatchSettingsChanged?.Invoke(this, EventArgs.Empty);

    partial void OnNmsOverlapThresholdChanged(double value)
        => MatchSettingsChanged?.Invoke(this, EventArgs.Empty);
}
