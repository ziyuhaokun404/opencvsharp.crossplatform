using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

/// <summary>
/// 灰度转换算子。
/// </summary>
public sealed class GrayscaleOperator : IImageOperator
{
    public ImageOperatorDescriptor Descriptor { get; } = new(
        "grayscale",
        "Grayscale",
        "颜色",
        "转换为单通道强度图。",
        [],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        using var gray = ToGray(source);
        var result = new Mat();
        Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ToGray(Mat source)
    {
        if (source.Channels() == 1)
            return source.Clone();

        var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
