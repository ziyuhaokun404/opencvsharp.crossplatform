using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Services;

/// <summary>
/// 基于窗口的图像文件对话框服务实现。
/// </summary>
public sealed class WindowImageFileDialogService : IImageFileDialogService
{
    private readonly AvaloniaWindow window;

    public WindowImageFileDialogService(AvaloniaWindow window)
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
