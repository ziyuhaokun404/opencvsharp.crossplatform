using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators.BuiltIn;

/// <summary>
/// 二值化阈值算子。
/// </summary>
public sealed class BinaryThresholdOperator : IImageOperator
{
    private const string ThresholdKey = "threshold";
    private const string MaxValueKey = "maxValue";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "binary-threshold",
        "Binary Threshold",
        "阈值",
        "将像素划分为前景和背景。",
        [
            OperatorParameter.Numeric(ThresholdKey, "阈值", 128, 0, 255),
            OperatorParameter.Numeric(MaxValueKey, "最大值", 255, 1, 255)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(ThresholdKey, 128);
        var paramB = parameters.GetValueOrDefault(MaxValueKey, 255);
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, paramA, Math.Max(1, paramB), ThresholdTypes.Binary);
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
}
