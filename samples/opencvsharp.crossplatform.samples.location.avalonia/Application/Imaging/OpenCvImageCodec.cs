using System;
using System.IO;
using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;

internal static class OpenCvImageCodec
{
    public static Mat Decode(byte[] bytes)
    {
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("无法解码图像数据。");

        return mat;
    }

    public static byte[] EncodePng(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        return bytes;
    }

    public static Bitmap CreateBitmap(byte[] bytes) => new(new MemoryStream(bytes));

    public static (int Width, int Height) ReadSize(byte[] bytes)
    {
        using var image = Decode(bytes);
        return (image.Width, image.Height);
    }
}
