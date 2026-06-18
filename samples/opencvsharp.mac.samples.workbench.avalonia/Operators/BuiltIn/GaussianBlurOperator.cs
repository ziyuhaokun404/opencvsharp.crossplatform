using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Operators.BuiltIn;

/// <summary>
/// 高斯模糊算子。
/// </summary>
public sealed class GaussianBlurOperator : IImageOperator
{
    private const string KernelKey = "kernel";
    private const string SigmaKey = "sigmaX10";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "gaussian-blur",
        "Gaussian Blur",
        "平滑",
        "使用高斯核抑制高频噪声。",
        [
            OperatorParameter.Numeric(KernelKey, "核大小", 7, 3, 61),
            OperatorParameter.Numeric(SigmaKey, "sigma ×10", 0, 0, 50)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(KernelKey, 7);
        var paramB = parameters.GetValueOrDefault(SigmaKey, 0);
        var kernel = OddInRange(paramA, 3, 61);
        var sigma = Math.Max(0, paramB) / 10.0;
        var result = new Mat();
        Cv2.GaussianBlur(source, result, new Size(kernel, kernel), sigma);
        return result;
    }

    private static int OddInRange(int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return clamped % 2 == 0 ? clamped + 1 <= max ? clamped + 1 : clamped - 1 : clamped;
    }
}
