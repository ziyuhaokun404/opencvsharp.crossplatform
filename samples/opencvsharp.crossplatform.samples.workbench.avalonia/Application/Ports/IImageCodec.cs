using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Imaging;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Ports;

public interface IImageCodec
{
    Mat Decode(byte[] bytes);

    ImageBuffer EncodePng(Mat image);

    ImageBuffer InspectPng(byte[] bytes);
}
