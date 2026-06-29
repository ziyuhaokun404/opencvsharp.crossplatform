namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Mapping;

public sealed record PyramidInfoState(
    string InfoText,
    bool Visible,
    string ModeText,
    string ScaleText,
    string SourceText,
    string TemplateText,
    string ResultText,
    string ThresholdText,
    string MinTemplateEdgeText)
{
    public static PyramidInfoState Empty { get; } = new(
        "",
        false,
        "-",
        "-",
        "-",
        "-",
        "-",
        "-",
        "-");
}
