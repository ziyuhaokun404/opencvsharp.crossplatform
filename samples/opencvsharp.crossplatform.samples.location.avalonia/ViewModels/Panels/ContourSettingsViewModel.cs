using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public sealed partial class ContourSettingsViewModel : ObservableObject
{
    private readonly ContourTemplateLocator contourLocator;
    private bool isUpdating;

    public ContourSettingsViewModel(ContourTemplateLocator contourLocator)
    {
        this.contourLocator = contourLocator;
    }

    public event EventHandler? SettingsChanged;

    public ContourTemplateLocator Locator => contourLocator;

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

    public ContourExtractionSettings CreateSettings()
        => new(
            MinContourArea,
            CannyLowThreshold,
            CannyHighThreshold,
            GradientThresholdScale,
            (int)CloseIterations,
            (int)MaxTemplateContours,
            MinimumTemplateContourCoverage,
            EdgeDistanceTolerance);

    public void ApplySettings(ContourExtractionSettings settings, bool notifyChanged = false)
    {
        isUpdating = true;
        MinContourArea = settings.MinimumContourArea;
        CannyLowThreshold = settings.CannyLowThreshold;
        CannyHighThreshold = settings.CannyHighThreshold;
        GradientThresholdScale = settings.GradientThresholdScale;
        CloseIterations = settings.CloseIterations;
        MaxTemplateContours = settings.MaximumTemplateContours;
        MinimumTemplateContourCoverage = settings.MinimumTemplateContourCoverage;
        EdgeDistanceTolerance = settings.EdgeDistanceTolerance;
        isUpdating = false;

        contourLocator.ApplySettings(settings);
        if (notifyChanged)
            SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnMinContourAreaChanged(double value) => UpdateLocator();
    partial void OnCannyLowThresholdChanged(double value) => UpdateLocator();
    partial void OnCannyHighThresholdChanged(double value) => UpdateLocator();
    partial void OnGradientThresholdScaleChanged(double value) => UpdateLocator();
    partial void OnCloseIterationsChanged(double value) => UpdateLocator();
    partial void OnMaxTemplateContoursChanged(double value) => UpdateLocator();
    partial void OnMinimumTemplateContourCoverageChanged(double value) => UpdateLocator();
    partial void OnEdgeDistanceToleranceChanged(double value) => UpdateLocator();

    private void UpdateLocator()
    {
        if (isUpdating)
            return;

        contourLocator.ApplySettings(CreateSettings());
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
