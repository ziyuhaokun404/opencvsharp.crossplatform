using System.Threading.Tasks;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Services;

/// <summary>
/// 图像文件对话框服务接口。
/// </summary>
public interface IImageFileDialogService
{
    Task<ImageFileResult?> OpenImageAsync();

    Task<string?> SavePngAsync(string suggestedFileName, byte[] bytes);
}

/// <summary>
/// 图像文件结果。
/// </summary>
public sealed record ImageFileResult(string Path, byte[] Bytes);
