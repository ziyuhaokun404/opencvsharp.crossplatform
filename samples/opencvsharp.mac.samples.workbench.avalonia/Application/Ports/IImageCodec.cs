using OpenCvSharp;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Imaging;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Ports;

public interface IImageCodec
{
    Mat Decode(byte[] bytes);

    ImageBuffer EncodePng(Mat image);

    ImageBuffer InspectPng(byte[] bytes);
}
