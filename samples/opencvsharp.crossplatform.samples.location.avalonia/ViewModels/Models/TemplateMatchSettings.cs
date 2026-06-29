using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateMatchSettings(
    byte[] SourceImageBytes,
    byte[] TemplateImageBytes,
    ContourExtractionSettings? ContourSettings = null);
