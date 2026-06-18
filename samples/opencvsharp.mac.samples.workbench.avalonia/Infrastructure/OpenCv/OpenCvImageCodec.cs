using System;
using OpenCvSharp;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Imaging;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Ports;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Infrastructure.OpenCv;

public sealed class OpenCvImageCodec : IImageCodec
{
    public Mat Decode(byte[] bytes)
    {
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("无法解码图像数据。");
        return mat;
    }

    public ImageBuffer EncodePng(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        return ImageBuffer.FromBytes(bytes, image.Width, image.Height, "png");
    }

    public ImageBuffer InspectPng(byte[] bytes)
    {
        using var mat = Decode(bytes);
        return ImageBuffer.FromBytes(bytes, mat.Width, mat.Height, "png");
    }
}
