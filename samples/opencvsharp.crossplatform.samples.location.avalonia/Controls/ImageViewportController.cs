using System;
using Avalonia;
using Avalonia.Threading;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaSize = Avalonia.Size;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;

/// <summary>
/// Manages zoom, pan, and animated viewport transforms for an image surface.
/// </summary>
public sealed class ImageViewportController : IDisposable
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 12.0;
    private const double WheelStep = 1.08;
    private const double ZoomAnimationMs = 120.0;
    private const double PanOverscroll = 96.0;

    private double zoom = 1.0;
    private double targetZoom = 1.0;
    private double zoomAnimationStartZoom = 1.0;
    private long zoomAnimationStartTicks;
    private AvaloniaPoint panOffset;
    private AvaloniaPoint targetPanOffset;
    private AvaloniaPoint zoomAnimationStartPanOffset;
    private AvaloniaPoint panStart;
    private AvaloniaPoint panStartOffset;
    private bool isPanning;
    private DispatcherTimer? zoomAnimationTimer;

    public event Action? ViewChanged;

    public bool IsPanning => isPanning;

    public void Reset()
    {
        zoom = 1.0;
        targetZoom = 1.0;
        panOffset = default;
        targetPanOffset = default;
        StopZoomAnimation();
        ViewChanged?.Invoke();
    }

    public void StopZoomAnimation()
    {
        zoomAnimationTimer?.Stop();
    }

    public void SyncAnimationTargets()
    {
        targetZoom = zoom;
        targetPanOffset = panOffset;
        StopZoomAnimation();
    }

    public ImageDisplayTransform? GetTransform(AvaloniaSize viewport, int imagePixelWidth, int imagePixelHeight)
    {
        var baseTransform = GetBaseTransform(viewport, imagePixelWidth, imagePixelHeight);
        if (baseTransform is null)
            return null;

        return new ImageDisplayTransform(
            baseTransform.Value.Scale * zoom,
            baseTransform.Value.ImageLeft + panOffset.X,
            baseTransform.Value.ImageTop + panOffset.Y,
            baseTransform.Value.Width * zoom,
            baseTransform.Value.Height * zoom);
    }

    public static ImageDisplayTransform? GetBaseTransform(AvaloniaSize viewport, int imagePixelWidth, int imagePixelHeight)
    {
        if (imagePixelWidth <= 0 || imagePixelHeight <= 0 || viewport.Width <= 0 || viewport.Height <= 0)
            return null;

        var scale = Math.Min(viewport.Width / imagePixelWidth, viewport.Height / imagePixelHeight);
        if (scale <= 0)
            return null;

        var imageWidth = imagePixelWidth * scale;
        var imageHeight = imagePixelHeight * scale;
        return new ImageDisplayTransform(
            scale,
            (viewport.Width - imageWidth) / 2,
            (viewport.Height - imageHeight) / 2,
            imageWidth,
            imageHeight);
    }

    public bool TryZoomAtPointer(
        double wheelDelta,
        AvaloniaPoint pointer,
        AvaloniaSize viewport,
        int imagePixelWidth,
        int imagePixelHeight)
    {
        var before = GetTransform(viewport, imagePixelWidth, imagePixelHeight);
        if (before is null)
            return false;

        var imagePoint = before.Value.DisplayToImage(pointer);
        var clampedDelta = Math.Clamp(wheelDelta, -3, 3);
        var factor = Math.Pow(WheelStep, clampedDelta);
        var nextZoom = Math.Clamp(targetZoom * factor, MinZoom, MaxZoom);

        var baseTransform = GetBaseTransform(viewport, imagePixelWidth, imagePixelHeight);
        if (baseTransform is null)
            return false;

        var nextPan = new AvaloniaPoint(
            pointer.X - baseTransform.Value.ImageLeft - imagePoint.X * baseTransform.Value.Scale * nextZoom,
            pointer.Y - baseTransform.Value.ImageTop - imagePoint.Y * baseTransform.Value.Scale * nextZoom);
        StartZoomAnimation(nextZoom, ClampPan(nextZoom, nextPan, viewport, imagePixelWidth, imagePixelHeight));
        return true;
    }

    public void BeginPan(AvaloniaPoint surfacePoint)
    {
        SyncAnimationTargets();
        isPanning = true;
        panStart = surfacePoint;
        panStartOffset = panOffset;
    }

    public void UpdatePan(AvaloniaPoint surfacePoint, AvaloniaSize viewport, int imagePixelWidth, int imagePixelHeight)
    {
        if (!isPanning)
            return;

        panOffset = panStartOffset + (surfacePoint - panStart);
        panOffset = ClampPan(zoom, panOffset, viewport, imagePixelWidth, imagePixelHeight);
        targetPanOffset = panOffset;
        targetZoom = zoom;
        ViewChanged?.Invoke();
    }

    public void EndPan()
    {
        isPanning = false;
    }

    public void CancelInteraction()
    {
        isPanning = false;
    }

    public void Dispose()
    {
        zoomAnimationTimer?.Stop();
        zoomAnimationTimer = null;
    }

    private void StartZoomAnimation(double nextZoom, AvaloniaPoint nextPan)
    {
        zoomAnimationStartZoom = zoom;
        zoomAnimationStartPanOffset = panOffset;
        targetZoom = nextZoom;
        targetPanOffset = nextPan;
        zoomAnimationStartTicks = Environment.TickCount64;

        zoomAnimationTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        zoomAnimationTimer.Tick -= ZoomAnimationTimer_Tick;
        zoomAnimationTimer.Tick += ZoomAnimationTimer_Tick;
        if (!zoomAnimationTimer.IsEnabled)
            zoomAnimationTimer.Start();
    }

    private void ZoomAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = Environment.TickCount64 - zoomAnimationStartTicks;
        var progress = Math.Clamp(elapsed / ZoomAnimationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        zoom = Lerp(zoomAnimationStartZoom, targetZoom, eased);
        panOffset = Lerp(zoomAnimationStartPanOffset, targetPanOffset, eased);
        ViewChanged?.Invoke();

        if (progress < 1)
            return;

        zoom = targetZoom;
        panOffset = targetPanOffset;
        ViewChanged?.Invoke();
        zoomAnimationTimer?.Stop();
    }

    private AvaloniaPoint ClampPan(double currentZoom, AvaloniaPoint offset, AvaloniaSize viewport, int imagePixelWidth, int imagePixelHeight)
    {
        var baseTransform = GetBaseTransform(viewport, imagePixelWidth, imagePixelHeight);
        if (baseTransform is null)
            return offset;

        var width = baseTransform.Value.Width * currentZoom;
        var height = baseTransform.Value.Height * currentZoom;
        var x = ClampAxisPan(offset.X, baseTransform.Value.ImageLeft, width, viewport.Width);
        var y = ClampAxisPan(offset.Y, baseTransform.Value.ImageTop, height, viewport.Height);
        return new AvaloniaPoint(x, y);
    }

    private static double ClampAxisPan(double pan, double baseOffset, double contentLength, double viewportLength)
    {
        var startAligned = -baseOffset;
        var endAligned = viewportLength - baseOffset - contentLength;
        var min = Math.Min(startAligned, endAligned) - PanOverscroll;
        var max = Math.Max(startAligned, endAligned) + PanOverscroll;
        return Math.Clamp(pan, min, max);
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;

    private static AvaloniaPoint Lerp(AvaloniaPoint from, AvaloniaPoint to, double progress) =>
        new(Lerp(from.X, to.X, progress), Lerp(from.Y, to.Y, progress));
}
