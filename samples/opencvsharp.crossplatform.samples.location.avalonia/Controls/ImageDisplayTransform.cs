using AvaloniaPoint = Avalonia.Point;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;

/// <summary>
/// Maps between image pixel coordinates and on-screen display coordinates for the source viewport.
/// </summary>
public readonly record struct ImageDisplayTransform(
    double Scale,
    double ImageLeft,
    double ImageTop,
    double Width,
    double Height)
{
    public AvaloniaPoint DisplayToImage(AvaloniaPoint point) =>
        new((point.X - ImageLeft) / Scale, (point.Y - ImageTop) / Scale);

    public AvaloniaPoint ImageToDisplay(AvaloniaPoint point) =>
        new(ImageLeft + point.X * Scale, ImageTop + point.Y * Scale);
}
