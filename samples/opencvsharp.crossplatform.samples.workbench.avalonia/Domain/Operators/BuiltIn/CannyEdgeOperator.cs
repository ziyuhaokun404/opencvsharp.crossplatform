using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

/// <summary>
/// Canny 边缘检测算子。
/// </summary>
public sealed class CannyEdgeOperator : IImageOperator
{
    private const string LowThresholdKey = "lowThreshold";
    private const string HighThresholdKey = "highThreshold";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "canny-edge",
        "Canny Edge",
        "边缘",
        "从灰度图中提取清晰边缘。",
        [
            OperatorParameter.Numeric(LowThresholdKey, "低阈值", 80, 0, 255),
            OperatorParameter.Numeric(HighThresholdKey, "高阈值", 160, 0, 255)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(LowThresholdKey, 80);
        var paramB = parameters.GetValueOrDefault(HighThresholdKey, 160);
        using var gray = ToGray(source);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, Math.Min(paramA, paramB), Math.Max(paramA, paramB));
        var result = new Mat();
        Cv2.CvtColor(edges, result, ColorConversionCodes.GRAY2BGR);
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
