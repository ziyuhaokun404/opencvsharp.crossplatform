using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators.BuiltIn;

/// <summary>
/// 自适应阈值算子。
/// </summary>
public sealed class AdaptiveThresholdOperator : IImageOperator
{
    private const string BlockSizeKey = "blockSize";
    private const string CKey = "c";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "adaptive-threshold",
        "Adaptive Threshold",
        "阈值",
        "用于光照不均场景的局部阈值。",
        [
            OperatorParameter.Numeric(BlockSizeKey, "块大小", 31, 3, 99),
            OperatorParameter.Numeric(CKey, "C", -2, -128, 127)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(BlockSizeKey, 31);
        var paramB = parameters.GetValueOrDefault(CKey, -2);
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        var blockSize = OddInRange(paramA, 3, 99);
        var c = Math.Clamp(paramB, -128, 127);
        Cv2.AdaptiveThreshold(gray, thresholded, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, blockSize, c);
        var result = new Mat();
        Cv2.CvtColor(thresholded, result, ColorConversionCodes.GRAY2BGR);
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

    private static int OddInRange(int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return clamped % 2 == 0 ? clamped + 1 <= max ? clamped + 1 : clamped - 1 : clamped;
    }
}
