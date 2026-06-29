using System;
using Avalonia.Controls;
using Avalonia.Input;
using OpenCvSharp.CrossPlatform.Samples.Shared;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Infrastructure.OpenCv;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Services;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.ViewModels;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Views;

public partial class MainWindow : AvaloniaWindow
{
    private bool isDraggingCompareHandle;

    static MainWindow()
    {
        OpenCvSharpNativeRuntime.Register();
    }

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel(
            new WindowImageFileDialogService(this),
            new OpenCvImageCodec());
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }

    private void SliderCompareHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control handle)
            return;

        isDraggingCompareHandle = true;
        e.Pointer.Capture(handle);
        UpdateCompareBlendFromPointer(e);
        e.Handled = true;
    }

    private void SliderCompareHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isDraggingCompareHandle)
            return;

        UpdateCompareBlendFromPointer(e);
        e.Handled = true;
    }

    private void SliderCompareHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isDraggingCompareHandle)
            return;

        isDraggingCompareHandle = false;
        e.Pointer.Capture(null);
        UpdateCompareBlendFromPointer(e);
        e.Handled = true;
    }

    private void UpdateCompareBlendFromPointer(PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || SliderCompareWell.Bounds.Width <= 0)
            return;

        var position = e.GetPosition(SliderCompareWell);
        viewModel.CompareBlend = position.X / SliderCompareWell.Bounds.Width;
    }
}
