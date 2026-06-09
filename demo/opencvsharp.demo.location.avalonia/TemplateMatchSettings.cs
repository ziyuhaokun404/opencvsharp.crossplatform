using OpenCvSharp.Core;

namespace OpenCvSharp.Demo.Location.Avalonia;

public sealed record TemplateMatchSettings(
    byte[] SourceImageBytes,
    byte[] TemplateImageBytes,
    ContourExtractionSettings? ContourSettings = null);
