using Avalonia.Media.Imaging;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.ViewModels.Models;

/// <summary>
/// 图像素材视图模型。
/// </summary>
public sealed record ImageAssetViewModel(
    string Name,
    string Source,
    string SizeText,
    string Note,
    Bitmap Bitmap,
    byte[] ImageBytes);
