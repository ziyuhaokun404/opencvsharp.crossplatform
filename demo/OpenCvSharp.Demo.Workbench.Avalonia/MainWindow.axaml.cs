using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using OpenCvSharp.Demo.Shared;
using Cv2 = OpenCvSharp.Cv2;

namespace OpenCvSharp.Demo.Workbench.Avalonia;

public partial class MainWindow : global::Avalonia.Controls.Window
{
    private bool isDraggingCompareHandle;

    static MainWindow()
    {
        OpenCvSharpNativeRuntime.Register();
    }

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel(new WindowImageFileDialogService(this));
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

internal sealed class WindowImageFileDialogService : IImageFileDialogService
{
    private readonly global::Avalonia.Controls.Window window;

    public WindowImageFileDialogService(global::Avalonia.Controls.Window window)
    {
        this.window = window;
    }

    public async Task<ImageFileResult?> OpenImageAsync()
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入图像",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图像文件")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tif", "*.tiff"],
                    MimeTypes = ["image/png", "image/jpeg", "image/bmp", "image/tiff"]
                }
            ]
        });

        if (files.Count == 0)
            return null;

        var file = files[0];
        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);

        return new ImageFileResult(file.Path.LocalPath, memory.ToArray());
    }

    public async Task<string?> SavePngAsync(string suggestedFileName, byte[] bytes)
    {
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出结果",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG 图像")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"]
                }
            ]
        });

        if (file is null)
            return null;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        return file.Name;
    }
}
