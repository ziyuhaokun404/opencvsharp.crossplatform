using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OpenCvSharp;
using OpenCvSharp.Demo.Shared;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaWindow = Avalonia.Controls.Window;
using Cv2 = OpenCvSharp.Cv2;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

public partial class MainWindow : AvaloniaWindow
{
    private const double DefaultRoiWidth = 160;
    private const double DefaultRoiHeight = 100;
    private const double MinCanvasZoom = 0.25;
    private const double MaxCanvasZoom = 12.0;
    private const double CanvasZoomWheelStep = 1.08;
    private const double CanvasZoomAnimationMs = 120.0;
    private const double CanvasPanOverscroll = 96.0;
    private bool isTemplateRoiMode;
    private bool isMovingRoi;
    private bool isRotatingRoi;
    private bool isResizingRoi;
    private bool isPanningCanvas;
    private AvaloniaPoint dragStart;
    private AvaloniaPoint roiStartPosition;
    private AvaloniaPoint resizeStart;
    private AvaloniaPoint panStart;
    private AvaloniaPoint panStartOffset;
    private double roiStartWidth;
    private double roiStartHeight;
    private double roiAngle;
    private double rotateStartAngle;
    private double roiStartAngle;
    private AvaloniaPoint roiCenterImage;
    private Size2f roiSizeImage;
    private double canvasZoom = 1.0;
    private double targetCanvasZoom = 1.0;
    private double zoomAnimationStartZoom = 1.0;
    private long zoomAnimationStartTicks;
    private AvaloniaPoint canvasPanOffset;
    private AvaloniaPoint targetCanvasPanOffset;
    private AvaloniaPoint zoomAnimationStartPanOffset;
    private DispatcherTimer? zoomAnimationTimer;

    static MainWindow()
    {
        OpenCvSharpNativeRuntime.Register();
    }

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel(new WindowImageFileDialogService(this));
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = viewModel;
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        SourceRoiSurface.AddHandler(PointerWheelChangedEvent, SourceRoiSurface_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerPressedEvent, SourceRoiSurface_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerMovedEvent, SourceRoiSurface_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerReleasedEvent, SourceRoiSurface_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerExitedEvent, SourceRoiSurface_PointerExited, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerCaptureLostEvent, SourceRoiSurface_PointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.SizeChanged += (_, _) => UpdateCanvasView();
        Opened += (_, _) => UpdateCanvasView();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.MatchOverlays))
        {
            HideTemplateRoi();
            UpdateMatchOverlays();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.SourceImageSource))
        {
            HideTemplateRoi();
            ResetCanvasView();
        }
    }

    private void CreateRoiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.SourcePixelWidth <= 0 || viewModel.SourcePixelHeight <= 0)
            return;

        var transform = GetImageDisplayTransform();
        if (transform is null)
            return;

        var width = Math.Min(DefaultRoiWidth, Math.Max(40, transform.Value.Width * 0.35));
        var height = Math.Min(DefaultRoiHeight, Math.Max(32, transform.Value.Height * 0.35));
        SetRoiSize(width, height);
        SetRoiPosition(transform.Value.ImageLeft + (transform.Value.Width - width) / 2, transform.Value.ImageTop + (transform.Value.Height - height) / 2);
        SetRoiAngle(0);
        SyncRoiImageStateFromDisplay(transform.Value);
        isTemplateRoiMode = true;
        SourceRoiLayer.IsVisible = true;
        SourceRoiSurface.Focus();
        Focus();
    }

    private void OpenBenchmarkChartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { BenchmarkSummary: { } summary })
            return;

        var window = new BenchmarkChartWindow(summary);
        window.Show(this);
    }

    private void OpenBenchmarkDetailButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { DetailedBenchmarkResult: { } result })
            return;

        var window = new BenchmarkDetailWindow(result);
        window.Show(this);
    }

    private void StabilityBenchmarkButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsRightButtonPress(e) || sender is not Control target || DataContext is not MainWindowViewModel viewModel)
            return;

        e.Handled = true;
        OpenBenchmarkRunCountMenu(
            target,
            "稳定性测试次数",
            viewModel.StabilityBenchmarkRunCount,
            value => viewModel.StabilityBenchmarkRunCount = value,
            () =>
            {
                if (viewModel.RunBenchmarkCommand.CanExecute(null))
                    viewModel.RunBenchmarkCommand.Execute(null);
            });
    }

    private void DetailedBenchmarkButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsRightButtonPress(e) || sender is not Control target || DataContext is not MainWindowViewModel viewModel)
            return;

        e.Handled = true;
        OpenBenchmarkRunCountMenu(
            target,
            "性能分析次数",
            viewModel.DetailedBenchmarkRunCount,
            value => viewModel.DetailedBenchmarkRunCount = value,
            () =>
            {
                if (viewModel.RunDetailedBenchmarkCommand.CanExecute(null))
                    viewModel.RunDetailedBenchmarkCommand.Execute(null);
            });
    }

    private void OpenBenchmarkRunCountMenu(
        Control target,
        string title,
        int currentRunCount,
        Action<int> setRunCount,
        Action runBenchmark)
    {
        var menu = new ContextMenu();
        var input = new NumericUpDown
        {
            Minimum = 1,
            Maximum = int.MaxValue,
            Increment = 1,
            Value = Math.Max(1, currentRunCount),
            FormatString = "0",
            Width = 120,
            ClipValueToMinMax = true
        };
        var titleText = new TextBlock { Text = title };
        titleText.Classes.Add("sectionTitle");

        var runButton = new Button
        {
            Content = "运行",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        runButton.Classes.Add("compact");

        void RunFromMenu()
        {
            if (input.Value is not { } value || value < 1 || value > int.MaxValue)
            {
                if (DataContext is MainWindowViewModel viewModel)
                    viewModel.StatusText = "测试次数必须大于 0。";
                return;
            }

            setRunCount((int)Math.Round(value));
            menu.Close();
            runBenchmark();
        }

        input.KeyDown += (_, args) =>
        {
            if (IsCommitKey(args.Key))
            {
                RunFromMenu();
                args.Handled = true;
            }
        };
        runButton.Click += (_, _) => RunFromMenu();

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(10),
            MinWidth = 150
        };
        panel.Children.Add(titleText);
        panel.Children.Add(input);
        panel.Children.Add(runButton);
        menu.Items.Add(panel);
        menu.Open(target);
        input.Focus();
    }

    private void SourceRoiSurface_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SourcePixelWidth: > 0, SourcePixelHeight: > 0 })
            return;

        var before = GetImageDisplayTransform();
        if (before is null)
            return;

        var pointer = e.GetPosition(SourceRoiSurface);
        var imagePoint = before.Value.DisplayToImage(pointer);
        var wheelDelta = Math.Clamp(e.Delta.Y, -3, 3);
        var factor = Math.Pow(CanvasZoomWheelStep, wheelDelta);
        var nextZoom = Math.Clamp(targetCanvasZoom * factor, MinCanvasZoom, MaxCanvasZoom);

        var after = GetBaseImageDisplayTransform();
        if (after is null)
            return;

        var nextPan = new AvaloniaPoint(
            pointer.X - after.Value.ImageLeft - imagePoint.X * after.Value.Scale * nextZoom,
            pointer.Y - after.Value.ImageTop - imagePoint.Y * after.Value.Scale * nextZoom);
        StartCanvasZoomAnimation(nextZoom, ClampCanvasPan(nextZoom, nextPan));
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (isPanningCanvas)
            return;

        var point = e.GetCurrentPoint(SourceRoiSurface);
        var properties = point.Properties;
        var updateKind = properties.PointerUpdateKind;
        var isControlClick = e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                             (properties.IsLeftButtonPressed || updateKind == PointerUpdateKind.LeftButtonPressed);
        var isImageSurfaceEvent = e.Source == SourceRoiSurface ||
                                  e.Source == ImageViewportLayer ||
                                  e.Source == SourceRoiImage ||
                                  e.Source == MatchOverlayLayer;
        var isLeftPan = !isTemplateRoiMode &&
                        isImageSurfaceEvent &&
                        (properties.IsLeftButtonPressed || updateKind == PointerUpdateKind.LeftButtonPressed);
        var isRightOrMiddlePan =
            properties.IsRightButtonPressed ||
            properties.IsMiddleButtonPressed ||
            isControlClick ||
            updateKind == PointerUpdateKind.RightButtonPressed ||
            updateKind == PointerUpdateKind.MiddleButtonPressed;

        if (!isLeftPan && !isRightOrMiddlePan)
            return;

        zoomAnimationTimer?.Stop();
        targetCanvasZoom = canvasZoom;
        targetCanvasPanOffset = canvasPanOffset;
        isPanningCanvas = true;
        panStart = e.GetPosition(SourceRoiSurface);
        panStartOffset = canvasPanOffset;
        e.Pointer.Capture(SourceRoiSurface);
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isPanningCanvas)
            return;

        var current = e.GetPosition(SourceRoiSurface);
        canvasPanOffset = panStartOffset + (current - panStart);
        canvasPanOffset = ClampCanvasPan(canvasZoom, canvasPanOffset);
        targetCanvasPanOffset = canvasPanOffset;
        targetCanvasZoom = canvasZoom;
        UpdateCanvasView();
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isPanningCanvas)
            return;

        isPanningCanvas = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerExited(object? sender, PointerEventArgs e)
    {
        // Keep panning while the pointer is captured. Cancelling on exit makes
        // Pointer capture may feel intermittent when the cursor leaves the image surface.
    }

    private void SourceRoiSurface_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        isPanningCanvas = false;
        isMovingRoi = false;
        isRotatingRoi = false;
        isResizingRoi = false;
    }

    private void SourceRoiAdorner_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLeftButtonPress(e))
            return;

        isMovingRoi = true;
        dragStart = e.GetPosition(SourceRoiSurface);
        roiStartPosition = new AvaloniaPoint(Canvas.GetLeft(SourceRoiAdorner), Canvas.GetTop(SourceRoiAdorner));
        e.Pointer.Capture(SourceRoiOverlay);
        e.Handled = true;
    }

    private void SourceRoiAdorner_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isMovingRoi)
            return;

        var current = e.GetPosition(SourceRoiSurface);
        var delta = current - dragStart;
        SetRoiPosition(roiStartPosition.X + delta.X, roiStartPosition.Y + delta.Y);
        SyncRoiImageStateFromDisplay();
        e.Handled = true;
    }

    private void SourceRoiAdorner_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isMovingRoi)
            return;

        isMovingRoi = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SourceRoiResizeHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLeftButtonPress(e))
            return;

        isResizingRoi = true;
        resizeStart = e.GetPosition(SourceRoiSurface);
        roiStartWidth = SourceRoiAdorner.Width;
        roiStartHeight = SourceRoiAdorner.Height;
        e.Pointer.Capture(SourceRoiResizeHandle);
        e.Handled = true;
    }

    private void SourceRoiResizeHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isResizingRoi)
            return;

        var delta = e.GetPosition(SourceRoiSurface) - resizeStart;
        var angle = roiAngle * Math.PI / 180;
        var widthDelta = delta.X * Math.Cos(angle) + delta.Y * Math.Sin(angle);
        var heightDelta = -delta.X * Math.Sin(angle) + delta.Y * Math.Cos(angle);
        SetRoiSize(Math.Max(32, roiStartWidth + widthDelta), Math.Max(24, roiStartHeight + heightDelta));
        SyncRoiImageStateFromDisplay();
        e.Handled = true;
    }

    private void SourceRoiResizeHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isResizingRoi)
            return;

        isResizingRoi = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SourceRoiRotateHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsLeftButtonPress(e))
            return;

        isRotatingRoi = true;
        rotateStartAngle = AngleToRoiCenter(e.GetPosition(SourceRoiSurface));
        roiStartAngle = roiAngle;
        e.Pointer.Capture(SourceRoiRotateHandle);
        e.Handled = true;
    }

    private void SourceRoiRotateHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isRotatingRoi)
            return;

        SetRoiAngle(roiStartAngle + AngleToRoiCenter(e.GetPosition(SourceRoiSurface)) - rotateStartAngle);
        e.Handled = true;
    }

    private void SourceRoiRotateHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isRotatingRoi)
            return;

        isRotatingRoi = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!isTemplateRoiMode)
            return;

        if (IsCommitKey(e.Key))
        {
            CommitRoiTemplate();
            e.Handled = true;
        }
        else if (IsCancelKey(e.Key))
        {
            HideTemplateRoi();
            e.Handled = true;
        }
    }

    private static bool IsCommitKey(Key key)
    {
        return key is Key.Enter or Key.Return;
    }

    private static bool IsCancelKey(Key key)
    {
        return key is Key.Escape;
    }

    private static bool IsLeftButtonPress(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        return point.Properties.IsLeftButtonPressed ||
               point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
    }

    private static bool IsRightButtonPress(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        return point.Properties.IsRightButtonPressed ||
               point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
    }

    private void CommitRoiTemplate()
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var transform = GetImageDisplayTransform();
        if (transform is null)
            return;

        SyncRoiImageStateFromDisplay(transform.Value);
        viewModel.SetTemplateFromSourceRotatedRoi(new RotatedRect(new Point2f((float)roiCenterImage.X, (float)roiCenterImage.Y), roiSizeImage, (float)roiAngle));
        HideTemplateRoi();
    }

    private void HideTemplateRoi()
    {
        isTemplateRoiMode = false;
        isMovingRoi = false;
        isRotatingRoi = false;
        isResizingRoi = false;
        SourceRoiLayer.IsVisible = false;
    }

    private void UpdateMatchOverlays()
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.MatchOverlays.Count == 0)
        {
            MatchOverlayLayer.Clear();
            return;
        }

        var transform = GetImageDisplayTransform();
        if (transform is null)
        {
            MatchOverlayLayer.Clear();
            return;
        }

        MatchOverlayLayer.Update(
            viewModel.MatchOverlays,
            transform.Value.Scale,
            transform.Value.ImageLeft,
            transform.Value.ImageTop);
    }

    private void UpdateCanvasView()
    {
        var transform = GetImageDisplayTransform();
        if (transform is null)
            return;

        SourceRoiImage.Width = transform.Value.Width;
        SourceRoiImage.Height = transform.Value.Height;
        Canvas.SetLeft(SourceRoiImage, transform.Value.ImageLeft);
        Canvas.SetTop(SourceRoiImage, transform.Value.ImageTop);
        UpdateMatchOverlays();
        UpdateRoiDisplayFromImageState(transform.Value);
    }

    private void ResetCanvasView()
    {
        canvasZoom = 1.0;
        targetCanvasZoom = 1.0;
        canvasPanOffset = default;
        targetCanvasPanOffset = default;
        zoomAnimationTimer?.Stop();
        UpdateCanvasView();
    }

    private AvaloniaPoint ClampCanvasPan(double zoom, AvaloniaPoint panOffset)
    {
        var baseTransform = GetBaseImageDisplayTransform();
        if (baseTransform is null)
            return panOffset;

        var surface = SourceRoiSurface.Bounds;
        var width = baseTransform.Value.Width * zoom;
        var height = baseTransform.Value.Height * zoom;
        var x = ClampAxisPan(panOffset.X, baseTransform.Value.ImageLeft, width, surface.Width);
        var y = ClampAxisPan(panOffset.Y, baseTransform.Value.ImageTop, height, surface.Height);

        return new AvaloniaPoint(x, y);
    }

    private static double ClampAxisPan(double pan, double baseOffset, double contentLength, double viewportLength)
    {
        var startAligned = -baseOffset;
        var endAligned = viewportLength - baseOffset - contentLength;
        var min = Math.Min(startAligned, endAligned) - CanvasPanOverscroll;
        var max = Math.Max(startAligned, endAligned) + CanvasPanOverscroll;
        return Math.Clamp(pan, min, max);
    }

    private void StartCanvasZoomAnimation(double nextZoom, AvaloniaPoint nextPan)
    {
        zoomAnimationStartZoom = canvasZoom;
        zoomAnimationStartPanOffset = canvasPanOffset;
        targetCanvasZoom = nextZoom;
        targetCanvasPanOffset = nextPan;
        zoomAnimationStartTicks = Environment.TickCount64;

        zoomAnimationTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        zoomAnimationTimer.Tick -= CanvasZoomAnimationTimer_Tick;
        zoomAnimationTimer.Tick += CanvasZoomAnimationTimer_Tick;
        if (!zoomAnimationTimer.IsEnabled)
            zoomAnimationTimer.Start();
    }

    private void CanvasZoomAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = Environment.TickCount64 - zoomAnimationStartTicks;
        var progress = Math.Clamp(elapsed / CanvasZoomAnimationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        canvasZoom = Lerp(zoomAnimationStartZoom, targetCanvasZoom, eased);
        canvasPanOffset = Lerp(zoomAnimationStartPanOffset, targetCanvasPanOffset, eased);
        UpdateCanvasView();

        if (progress < 1)
            return;

        canvasZoom = targetCanvasZoom;
        canvasPanOffset = targetCanvasPanOffset;
        UpdateCanvasView();
        zoomAnimationTimer?.Stop();
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + (to - from) * progress;
    }

    private static AvaloniaPoint Lerp(AvaloniaPoint from, AvaloniaPoint to, double progress)
    {
        return new AvaloniaPoint(
            Lerp(from.X, to.X, progress),
            Lerp(from.Y, to.Y, progress));
    }

    private void SetRoiPosition(double left, double top)
    {
        Canvas.SetLeft(SourceRoiAdorner, left);
        Canvas.SetTop(SourceRoiAdorner, top);
    }

    private void SetRoiSize(double width, double height)
    {
        SourceRoiAdorner.Width = width;
        SourceRoiAdorner.Height = height;
        SourceRoiOverlay.Width = width;
        SourceRoiOverlay.Height = height;
        Canvas.SetLeft(SourceRoiRotateHandle, width / 2 - SourceRoiRotateHandle.Width / 2);
        Canvas.SetTop(SourceRoiRotateHandle, -22);
        Canvas.SetLeft(SourceRoiResizeHandle, width - SourceRoiResizeHandle.Width / 2);
        Canvas.SetTop(SourceRoiResizeHandle, height - SourceRoiResizeHandle.Height / 2);
    }

    private void SetRoiAngle(double angle)
    {
        roiAngle = angle;
        if (SourceRoiAdorner.RenderTransform is RotateTransform transform)
            transform.Angle = angle;
    }

    private void SyncRoiImageStateFromDisplay()
    {
        var transform = GetImageDisplayTransform();
        if (transform is null)
            return;

        SyncRoiImageStateFromDisplay(transform.Value);
    }

    private void SyncRoiImageStateFromDisplay(ImageDisplayTransform transform)
    {
        var centerDisplay = new AvaloniaPoint(
            Canvas.GetLeft(SourceRoiAdorner) + SourceRoiAdorner.Width / 2,
            Canvas.GetTop(SourceRoiAdorner) + SourceRoiAdorner.Height / 2);
        roiCenterImage = transform.DisplayToImage(centerDisplay);
        roiSizeImage = new Size2f(
            (float)(SourceRoiAdorner.Width / transform.Scale),
            (float)(SourceRoiAdorner.Height / transform.Scale));
    }

    private void UpdateRoiDisplayFromImageState(ImageDisplayTransform transform)
    {
        if (!isTemplateRoiMode)
            return;

        var width = roiSizeImage.Width * transform.Scale;
        var height = roiSizeImage.Height * transform.Scale;
        var centerDisplay = transform.ImageToDisplay(roiCenterImage);
        SetRoiSize(width, height);
        SetRoiPosition(centerDisplay.X - width / 2, centerDisplay.Y - height / 2);
    }

    private double AngleToRoiCenter(AvaloniaPoint position)
    {
        var center = new AvaloniaPoint(
            Canvas.GetLeft(SourceRoiAdorner) + SourceRoiAdorner.Width / 2,
            Canvas.GetTop(SourceRoiAdorner) + SourceRoiAdorner.Height / 2);
        return Math.Atan2(position.Y - center.Y, position.X - center.X) * 180 / Math.PI;
    }

    private ImageDisplayTransform? GetImageDisplayTransform()
    {
        var baseTransform = GetBaseImageDisplayTransform();
        if (baseTransform is null)
            return null;

        return new ImageDisplayTransform(
            baseTransform.Value.Scale * canvasZoom,
            baseTransform.Value.ImageLeft + canvasPanOffset.X,
            baseTransform.Value.ImageTop + canvasPanOffset.Y,
            baseTransform.Value.Width * canvasZoom,
            baseTransform.Value.Height * canvasZoom);
    }

    private ImageDisplayTransform? GetBaseImageDisplayTransform()
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.SourcePixelWidth <= 0 || viewModel.SourcePixelHeight <= 0)
            return null;

        var display = SourceRoiSurface.Bounds;
        if (display.Width <= 0 || display.Height <= 0)
            return null;

        var scale = Math.Min(display.Width / viewModel.SourcePixelWidth, display.Height / viewModel.SourcePixelHeight);
        if (scale <= 0)
            return null;

        var imageWidth = viewModel.SourcePixelWidth * scale;
        var imageHeight = viewModel.SourcePixelHeight * scale;
        return new ImageDisplayTransform(scale, (display.Width - imageWidth) / 2, (display.Height - imageHeight) / 2, imageWidth, imageHeight);
    }

    private readonly record struct ImageDisplayTransform(double Scale, double ImageLeft, double ImageTop, double Width, double Height)
    {
        public AvaloniaPoint DisplayToImage(AvaloniaPoint point) => new((point.X - ImageLeft) / Scale, (point.Y - ImageTop) / Scale);

        public AvaloniaPoint ImageToDisplay(AvaloniaPoint point) => new(ImageLeft + point.X * Scale, ImageTop + point.Y * Scale);
    }

}
