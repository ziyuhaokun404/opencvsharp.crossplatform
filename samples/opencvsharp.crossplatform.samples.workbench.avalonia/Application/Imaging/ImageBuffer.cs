using System.Linq;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Imaging;

public sealed record ImageBuffer(byte[] Bytes, int Width, int Height, string Format)
{
    public string SizeText => $"{Width} x {Height}";

    public static ImageBuffer FromBytes(byte[] bytes, int width, int height, string format)
    {
        return new ImageBuffer(bytes.ToArray(), width, height, format);
    }
}
