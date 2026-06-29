using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;

/// <summary>
/// 高性能匹配结果叠加层：使用 DrawingContext 直接绘制所有矩形，
/// 替代为每个匹配创建独立 Border 控件的方式。
/// 当匹配数量达到数千时，可以避免大量 UI 元素的创建、布局和渲染开销。
/// </summary>
public sealed class MatchOverlayControl : Control
{
    private static readonly IPen OverlayPen = new Pen(
        new SolidColorBrush(Color.Parse("#2563EB")).ToImmutable(), 3);

    private static readonly IBrush OverlayFill =
        new SolidColorBrush(Color.Parse("#1A2563EB")).ToImmutable();

    private IReadOnlyList<MatchOverlayViewModel>? _overlays;
    private double _scale;
    private double _imageLeft;
    private double _imageTop;

    /// <summary>
    /// 更新叠加层数据和变换参数，触发重绘。
    /// 仅调用 InvalidateVisual()，不创建任何 UI 元素。
    /// </summary>
    public void Update(
        IReadOnlyList<MatchOverlayViewModel>? overlays,
        double scale, double imageLeft, double imageTop)
    {
        _overlays = overlays;
        _scale = scale;
        _imageLeft = imageLeft;
        _imageTop = imageTop;
        InvalidateVisual();
    }

    /// <summary>
    /// 清除叠加层。
    /// </summary>
    public void Clear()
    {
        _overlays = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_overlays is null || _overlays.Count == 0)
            return;

        foreach (var overlay in _overlays)
        {
            if (Math.Abs(overlay.Angle) < 0.01)
            {
                var rect = new global::Avalonia.Rect(
                    _imageLeft + overlay.X * _scale,
                    _imageTop + overlay.Y * _scale,
                    overlay.Width * _scale,
                    overlay.Height * _scale);
                context.DrawRectangle(OverlayFill, OverlayPen, rect);
                continue;
            }

            DrawRotatedRectangle(context, overlay);
        }
    }

    private void DrawRotatedRectangle(DrawingContext context, MatchOverlayViewModel overlay)
    {
        var centerX = _imageLeft + (overlay.X + overlay.Width / 2.0) * _scale;
        var centerY = _imageTop + (overlay.Y + overlay.Height / 2.0) * _scale;
        var halfWidth = overlay.Width * _scale / 2.0;
        var halfHeight = overlay.Height * _scale / 2.0;
        var radians = overlay.Angle * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var p0 = RotatePoint(centerX, centerY, -halfWidth, -halfHeight, cos, sin);
            var p1 = RotatePoint(centerX, centerY, halfWidth, -halfHeight, cos, sin);
            var p2 = RotatePoint(centerX, centerY, halfWidth, halfHeight, cos, sin);
            var p3 = RotatePoint(centerX, centerY, -halfWidth, halfHeight, cos, sin);
            ctx.BeginFigure(p0, isFilled: true);
            ctx.LineTo(p1);
            ctx.LineTo(p2);
            ctx.LineTo(p3);
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(OverlayFill, OverlayPen, geometry);
    }

    private static global::Avalonia.Point RotatePoint(double centerX, double centerY, double localX, double localY, double cos, double sin)
    {
        return new global::Avalonia.Point(
            centerX + localX * cos - localY * sin,
            centerY + localX * sin + localY * cos);
    }
}
