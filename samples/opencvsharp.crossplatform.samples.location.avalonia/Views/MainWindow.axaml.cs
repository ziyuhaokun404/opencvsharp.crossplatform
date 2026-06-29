using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;
using OpenCvSharp.CrossPlatform.Samples.Shared;
using OpenCvSharp.CrossPlatform.Samples.Shared.Services;
using AvaloniaSize = Avalonia.Size;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Views;

public partial class MainWindow : AvaloniaWindow
{
    private readonly ImageViewportController viewport = new();
    private MainWindowViewModel? viewModel;

    static MainWindow()
    {
        OpenCvSharpNativeRuntime.Register();
    }

    public MainWindow()
    {
        InitializeComponent();
        TemplateRoiEditor.CoordinateRoot = SourceRoiSurface;
        viewModel = new MainWindowViewModel(new WindowImageFileDialogService(this));
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.Result.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = viewModel;
        viewport.ViewChanged += UpdateCanvasView;
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        SourceRoiSurface.AddHandler(PointerWheelChangedEvent, SourceRoiSurface_PointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerPressedEvent, SourceRoiSurface_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerMovedEvent, SourceRoiSurface_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerReleasedEvent, SourceRoiSurface_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerExitedEvent, SourceRoiSurface_PointerExited, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.AddHandler(PointerCaptureLostEvent, SourceRoiSurface_PointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        SourceRoiSurface.SizeChanged += (_, _) => UpdateCanvasView();
        Opened += (_, _) => UpdateCanvasView();
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        viewport.ViewChanged -= UpdateCanvasView;
        viewport.Dispose();
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.Result.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.Dispose();
            viewModel = null;
        }

        DataContext = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchResultViewModel.MatchOverlays))
        {
            TemplateRoiEditor.Hide();
            UpdateMatchOverlays();
        }
        else if (sender is MainWindowViewModel && e.PropertyName == nameof(MainWindowViewModel.SourceImageSource))
        {
            TemplateRoiEditor.Hide();
            ResetCanvasView();
        }
    }

    private AvaloniaSize GetViewportSize() => SourceRoiSurface.Bounds.Size;

    private bool TryGetImageDimensions(out int width, out int height)
    {
        if (DataContext is MainWindowViewModel { SourcePixelWidth: > 0, SourcePixelHeight: > 0 } vm)
        {
            width = vm.SourcePixelWidth;
            height = vm.SourcePixelHeight;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private void CreateRoiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SourcePixelWidth <= 0 || vm.SourcePixelHeight <= 0)
            return;

        var transform = GetImageDisplayTransform();
        if (transform is null)
            return;

        TemplateRoiEditor.Begin(transform.Value);
        SourceRoiSurface.Focus();
        Focus();
    }

    private void OpenBenchmarkChartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { Benchmark.Summary: { } summary })
            return;

        var window = new BenchmarkChartWindow(summary);
        window.Show(this);
    }

    private void OpenBenchmarkDetailButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { Benchmark.DetailedResult: { } result })
            return;

        var window = new BenchmarkDetailWindow(result);
        window.Show(this);
    }

    private void StabilityBenchmarkButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsRightButtonPress(e) || sender is not Control target || DataContext is not MainWindowViewModel vm)
            return;

        e.Handled = true;
        OpenBenchmarkRunCountMenu(
            target,
            "稳定性测试次数",
            vm.Benchmark.StabilityRunCount,
            value => vm.Benchmark.StabilityRunCount = value,
            () =>
            {
                if (vm.Benchmark.RunStabilityBenchmarkCommand.CanExecute(null))
                    vm.Benchmark.RunStabilityBenchmarkCommand.Execute(null);
            });
    }

    private void DetailedBenchmarkButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsRightButtonPress(e) || sender is not Control target || DataContext is not MainWindowViewModel vm)
            return;

        e.Handled = true;
        OpenBenchmarkRunCountMenu(
            target,
            "性能分析次数",
            vm.Benchmark.DetailedRunCount,
            value => vm.Benchmark.DetailedRunCount = value,
            () =>
            {
                if (vm.Benchmark.RunDetailedBenchmarkCommand.CanExecute(null))
                    vm.Benchmark.RunDetailedBenchmarkCommand.Execute(null);
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
                if (DataContext is MainWindowViewModel vm)
                    vm.StatusText = "测试次数必须大于 0。";
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
        if (!TryGetImageDimensions(out var width, out var height))
            return;

        var pointer = e.GetPosition(SourceRoiSurface);
        if (viewport.TryZoomAtPointer(e.Delta.Y, pointer, GetViewportSize(), width, height))
            e.Handled = true;
    }

    private void SourceRoiSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (viewport.IsPanning)
            return;

        var point = e.GetCurrentPoint(SourceRoiSurface);
        var properties = point.Properties;
        var updateKind = properties.PointerUpdateKind;
        var isControlClick = e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                             (properties.IsLeftButtonPressed || updateKind == PointerUpdateKind.LeftButtonPressed);
        var isImageSurfaceEvent = e.Source == SourceRoiSurface ||
                                  e.Source == ImageViewportLayer ||
                                  e.Source == SourceRoiImage ||
                                  e.Source == MatchOverlayLayer ||
                                  e.Source == TemplateRoiEditor;
        var isLeftPan = !TemplateRoiEditor.IsActive &&
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

        viewport.BeginPan(e.GetPosition(SourceRoiSurface));
        e.Pointer.Capture(SourceRoiSurface);
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!viewport.IsPanning || !TryGetImageDimensions(out var width, out var height))
            return;

        viewport.UpdatePan(e.GetPosition(SourceRoiSurface), GetViewportSize(), width, height);
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!viewport.IsPanning)
            return;

        viewport.EndPan();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SourceRoiSurface_PointerExited(object? sender, PointerEventArgs e)
    {
        // Keep panning while the pointer is captured.
    }

    private void SourceRoiSurface_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        viewport.CancelInteraction();
        TemplateRoiEditor.CancelInteraction();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!TemplateRoiEditor.IsActive)
            return;

        if (IsCommitKey(e.Key))
        {
            CommitRoiTemplate();
            e.Handled = true;
        }
        else if (IsCancelKey(e.Key))
        {
            TemplateRoiEditor.Hide();
            e.Handled = true;
        }
    }

    private static bool IsCommitKey(Key key) => key is Key.Enter or Key.Return;

    private static bool IsCancelKey(Key key) => key is Key.Escape;

    private static bool IsRightButtonPress(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(null);
        return point.Properties.IsRightButtonPressed ||
               point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
    }

    private void CommitRoiTemplate()
    {
        if (DataContext is not MainWindowViewModel vm || !TemplateRoiEditor.TryGetRotatedRect(out var rect))
            return;

        vm.SetTemplateFromSourceRotatedRoi(rect);
        TemplateRoiEditor.Hide();
    }

    private void UpdateMatchOverlays()
    {
        if (DataContext is not MainWindowViewModel vm ||
            vm.Result.MatchOverlays.Count == 0)
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
            vm.Result.MatchOverlays,
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
        TemplateRoiEditor.ApplyTransform(transform.Value);
    }

    private void ResetCanvasView() => viewport.Reset();

    private ImageDisplayTransform? GetImageDisplayTransform()
    {
        if (!TryGetImageDimensions(out var width, out var height))
            return null;

        return viewport.GetTransform(GetViewportSize(), width, height);
    }
}
