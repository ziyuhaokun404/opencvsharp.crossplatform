using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using OpenCvSharp;
using AvaloniaPoint = Avalonia.Point;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;

/// <summary>
/// Interactive rotated-rectangle ROI editor for template cropping from the source image.
/// </summary>
public partial class TemplateRoiEditorControl : UserControl
{
    private const double DefaultWidth = 160;
    private const double DefaultHeight = 100;
    private const double MinRoiWidth = 32;
    private const double MinRoiHeight = 24;

    private bool isMoving;
    private bool isRotating;
    private bool isResizing;
    private AvaloniaPoint dragStart;
    private AvaloniaPoint roiStartPosition;
    private AvaloniaPoint resizeStart;
    private double roiStartWidth;
    private double roiStartHeight;
    private double angle;
    private double rotateStartAngle;
    private double roiStartAngle;
    private AvaloniaPoint centerImage;
    private Size2f sizeImage;
    private double lastTransformScale;
    private double lastImageLeft;
    private double lastImageTop;

    public TemplateRoiEditorControl()
    {
        InitializeComponent();
        WirePointerHandlers(Overlay, OnMovePressed, OnMoveMoved, OnMoveReleased);
        WirePointerHandlers(ResizeHandle, OnResizePressed, OnResizeMoved, OnResizeReleased);
        WirePointerHandlers(RotateHandle, OnRotatePressed, OnRotateMoved, OnRotateReleased);
    }

    public bool IsActive { get; private set; }

    public bool IsManipulating => isMoving || isRotating || isResizing;

    /// <summary>Coordinate root for pointer positions (typically the image surface grid).</summary>
    public Visual? CoordinateRoot { get; set; }

    public void Begin(ImageDisplayTransform transform, double? displayWidth = null, double? displayHeight = null)
    {
        CacheTransform(transform);

        var width = displayWidth ?? Math.Min(DefaultWidth, Math.Max(MinRoiWidth, transform.Width * 0.35));
        var height = displayHeight ?? Math.Min(DefaultHeight, Math.Max(MinRoiHeight, transform.Height * 0.35));
        angle = 0;
        SetRotateAngle(0);

        SetDisplaySize(width, height);
        SetDisplayPosition(
            transform.ImageLeft + (transform.Width - width) / 2,
            transform.ImageTop + (transform.Height - height) / 2);
        SyncImageStateFromDisplay();

        IsActive = true;
        IsVisible = true;
    }

    public void Hide()
    {
        IsActive = false;
        IsVisible = false;
        CancelInteraction();
    }

    public void ApplyTransform(ImageDisplayTransform transform)
    {
        CacheTransform(transform);
        if (!IsActive)
            return;

        var width = sizeImage.Width * transform.Scale;
        var height = sizeImage.Height * transform.Scale;
        var centerDisplay = transform.ImageToDisplay(centerImage);
        SetDisplaySize(width, height);
        SetDisplayPosition(centerDisplay.X - width / 2, centerDisplay.Y - height / 2);
    }

    public bool TryGetRotatedRect(out RotatedRect rect)
    {
        if (!IsActive)
        {
            rect = default;
            return false;
        }

        rect = new RotatedRect(
            new Point2f((float)centerImage.X, (float)centerImage.Y),
            sizeImage,
            (float)angle);
        return true;
    }

    public void CancelInteraction()
    {
        isMoving = false;
        isRotating = false;
        isResizing = false;
    }

    private void CacheTransform(ImageDisplayTransform transform)
    {
        lastTransformScale = transform.Scale;
        lastImageLeft = transform.ImageLeft;
        lastImageTop = transform.ImageTop;
    }

    private void WirePointerHandlers(
        InputElement target,
        EventHandler<PointerPressedEventArgs> onPressed,
        EventHandler<PointerEventArgs> onMoved,
        EventHandler<PointerReleasedEventArgs> onReleased)
    {
        target.PointerPressed += onPressed;
        target.PointerMoved += onMoved;
        target.PointerReleased += onReleased;
    }

    private AvaloniaPoint GetSurfacePoint(PointerEventArgs e) =>
        e.GetPosition(CoordinateRoot ?? this);

    private void OnMovePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsActive || !IsLeftButtonPress(e))
            return;

        isMoving = true;
        dragStart = GetSurfacePoint(e);
        roiStartPosition = new AvaloniaPoint(Canvas.GetLeft(Adorner), Canvas.GetTop(Adorner));
        e.Pointer.Capture(Overlay);
        e.Handled = true;
    }

    private void OnMoveMoved(object? sender, PointerEventArgs e)
    {
        if (!isMoving)
            return;

        var delta = GetSurfacePoint(e) - dragStart;
        SetDisplayPosition(roiStartPosition.X + delta.X, roiStartPosition.Y + delta.Y);
        SyncImageStateFromDisplay();
        e.Handled = true;
    }

    private void OnMoveReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isMoving)
            return;

        isMoving = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnResizePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsActive || !IsLeftButtonPress(e))
            return;

        isResizing = true;
        resizeStart = GetSurfacePoint(e);
        roiStartWidth = Adorner.Width;
        roiStartHeight = Adorner.Height;
        e.Pointer.Capture(ResizeHandle);
        e.Handled = true;
    }

    private void OnResizeMoved(object? sender, PointerEventArgs e)
    {
        if (!isResizing)
            return;

        var delta = GetSurfacePoint(e) - resizeStart;
        var radians = angle * Math.PI / 180;
        var widthDelta = delta.X * Math.Cos(radians) + delta.Y * Math.Sin(radians);
        var heightDelta = -delta.X * Math.Sin(radians) + delta.Y * Math.Cos(radians);
        SetDisplaySize(
            Math.Max(MinRoiWidth, roiStartWidth + widthDelta),
            Math.Max(MinRoiHeight, roiStartHeight + heightDelta));
        SyncImageStateFromDisplay();
        e.Handled = true;
    }

    private void OnResizeReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isResizing)
            return;

        isResizing = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnRotatePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsActive || !IsLeftButtonPress(e))
            return;

        isRotating = true;
        rotateStartAngle = AngleToCenter(GetSurfacePoint(e));
        roiStartAngle = angle;
        e.Pointer.Capture(RotateHandle);
        e.Handled = true;
    }

    private void OnRotateMoved(object? sender, PointerEventArgs e)
    {
        if (!isRotating)
            return;

        SetAngle(roiStartAngle + AngleToCenter(GetSurfacePoint(e)) - rotateStartAngle);
        e.Handled = true;
    }

    private void OnRotateReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isRotating)
            return;

        isRotating = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SetDisplayPosition(double left, double top)
    {
        Canvas.SetLeft(Adorner, left);
        Canvas.SetTop(Adorner, top);
    }

    private void SetDisplaySize(double width, double height)
    {
        Adorner.Width = width;
        Adorner.Height = height;
        Overlay.Width = width;
        Overlay.Height = height;
        Canvas.SetLeft(RotateHandle, width / 2 - RotateHandle.Width / 2);
        Canvas.SetTop(RotateHandle, -22);
        Canvas.SetLeft(ResizeHandle, width - ResizeHandle.Width / 2);
        Canvas.SetTop(ResizeHandle, height - ResizeHandle.Height / 2);
    }

    private void SetAngle(double value)
    {
        angle = value;
        SetRotateAngle(value);
    }

    private void SetRotateAngle(double value)
    {
        if (Adorner.RenderTransform is RotateTransform rotate)
            rotate.Angle = value;
    }

    private void SyncImageStateFromDisplay()
    {
        if (lastTransformScale <= 0)
            return;

        var centerDisplay = new AvaloniaPoint(
            Canvas.GetLeft(Adorner) + Adorner.Width / 2,
            Canvas.GetTop(Adorner) + Adorner.Height / 2);
        centerImage = new AvaloniaPoint(
            (centerDisplay.X - lastImageLeft) / lastTransformScale,
            (centerDisplay.Y - lastImageTop) / lastTransformScale);
        sizeImage = new Size2f(
            (float)(Adorner.Width / lastTransformScale),
            (float)(Adorner.Height / lastTransformScale));
    }

    private double AngleToCenter(AvaloniaPoint position)
    {
        var center = new AvaloniaPoint(
            Canvas.GetLeft(Adorner) + Adorner.Width / 2,
            Canvas.GetTop(Adorner) + Adorner.Height / 2);
        return Math.Atan2(position.Y - center.Y, position.X - center.X) * 180 / Math.PI;
    }

    private static bool IsLeftButtonPress(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        return point.Properties.IsLeftButtonPressed ||
               point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
    }
}
