using OpenCvSharp.Core;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

public sealed record TemplateMatchSettings(
    byte[] SourceImageBytes,
    byte[] TemplateImageBytes,
    ContourExtractionSettings? ContourSettings = null);
