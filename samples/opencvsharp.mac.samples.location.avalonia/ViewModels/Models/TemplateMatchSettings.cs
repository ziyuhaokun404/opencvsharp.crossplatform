using OpenCvSharp.Mac.Core;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateMatchSettings(
    byte[] SourceImageBytes,
    byte[] TemplateImageBytes,
    ContourExtractionSettings? ContourSettings = null);
