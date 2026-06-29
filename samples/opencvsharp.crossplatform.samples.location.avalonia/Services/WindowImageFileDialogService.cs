using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using AvaloniaWindow = Avalonia.Controls.Window;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Services;

public sealed class WindowImageFileDialogService
{
    private readonly AvaloniaWindow window;

    public WindowImageFileDialogService(AvaloniaWindow window)
    {
        this.window = window;
    }

    public async Task<ImageFileResult?> OpenImageAsync(string title)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
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
}
