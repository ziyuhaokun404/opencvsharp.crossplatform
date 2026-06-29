using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Mapping;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public sealed partial class PyramidInfoViewModel : ObservableObject
{
    [ObservableProperty]
    private string infoText = "";

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private string modeText = "-";

    [ObservableProperty]
    private string scaleText = "-";

    [ObservableProperty]
    private string sourceText = "-";

    [ObservableProperty]
    private string templateText = "-";

    [ObservableProperty]
    private string resultText = "-";

    [ObservableProperty]
    private string thresholdText = "-";

    [ObservableProperty]
    private string minTemplateEdgeText = "-";

    public void Apply(TemplateLocatorResult result)
        => Apply(MatchResultMapper.CreatePyramidInfo(result));

    public void Apply(PyramidInfoState state)
    {
        InfoText = state.InfoText;
        Visible = state.Visible;
        ModeText = state.ModeText;
        ScaleText = state.ScaleText;
        SourceText = state.SourceText;
        TemplateText = state.TemplateText;
        ResultText = state.ResultText;
        ThresholdText = state.ThresholdText;
        MinTemplateEdgeText = state.MinTemplateEdgeText;
    }

    public void Clear() => Apply(PyramidInfoState.Empty);
}
