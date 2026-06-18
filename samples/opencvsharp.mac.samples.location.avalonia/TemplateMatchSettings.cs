using OpenCvSharp.Mac.Core;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia;

public sealed record TemplateMatchSettings(
    byte[] SourceImageBytes,
    byte[] TemplateImageBytes,
    ContourExtractionSettings? ContourSettings = null);
