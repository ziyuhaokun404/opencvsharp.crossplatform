using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

/// <summary>
/// 锐化算子。
/// </summary>
public sealed class SharpenOperator : IImageOperator
{
    private const string AmountKey = "amount";
    private const string KernelKey = "kernel";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "sharpen",
        "Sharpen",
        "增强",
        "基于反锐化掩模增强细节。",
        [
            OperatorParameter.Numeric(AmountKey, "强度", 120, 5, 300),
            OperatorParameter.Numeric(KernelKey, "模糊核", 5, 3, 41)
        ],
        true);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(AmountKey, 120);
        var paramB = parameters.GetValueOrDefault(KernelKey, 5);
        var kernel = OddInRange(paramB, 3, 41);
        var amount = Math.Clamp(paramA / 100.0, 0.05, 3.0);
        using var blurred = new Mat();
        Cv2.GaussianBlur(source, blurred, new Size(kernel, kernel), 0);

        var result = new Mat();
        Cv2.AddWeighted(source, 1.0 + amount, blurred, -amount, 0, result);

        if (!context.ClampOutput)
            return result;

        var clamped = new Mat();
        result.ConvertTo(clamped, MatType.CV_8UC3);
        result.Dispose();
        return clamped;
    }

    private static int OddInRange(int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return clamped % 2 == 0 ? clamped + 1 <= max ? clamped + 1 : clamped - 1 : clamped;
    }
}
