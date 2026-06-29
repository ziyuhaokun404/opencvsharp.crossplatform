using System.Threading.Tasks;

namespace OpenCvSharp.CrossPlatform.Samples.Shared.Services;

/// <summary>
/// Image file dialog service for Avalonia sample apps.
/// </summary>
public interface IImageFileDialogService
{
    Task<ImageFileResult?> OpenImageAsync(string? title = null);

    Task<string?> SavePngAsync(string suggestedFileName, byte[] bytes);
}

/// <summary>
/// Result of an image file open dialog.
/// </summary>
public sealed record ImageFileResult(string Path, byte[] Bytes);
