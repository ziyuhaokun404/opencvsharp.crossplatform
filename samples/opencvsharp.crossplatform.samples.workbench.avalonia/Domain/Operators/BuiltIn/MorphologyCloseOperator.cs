using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

/// <summary>
/// 形态学闭运算算子。
/// </summary>
public sealed class MorphologyCloseOperator : IImageOperator
{
    private const string KernelKey = "kernel";
    private const string IterationsKey = "iterations";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "morphology-close",
        "Morphology Close",
        "形态学",
        "闭合二值区域中的小孔和间隙。",
        [
            OperatorParameter.Numeric(KernelKey, "核大小", 7, 3, 41),
            OperatorParameter.Numeric(IterationsKey, "迭代次数", 2, 1, 12)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(KernelKey, 7);
        var paramB = parameters.GetValueOrDefault(IterationsKey, 2);
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(OddInRange(paramA, 3, 41), OddInRange(paramA, 3, 41)));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresholded, closed, MorphTypes.Close, kernel, iterations: Math.Clamp(paramB, 1, 12));

        var result = new Mat();
        Cv2.CvtColor(closed, result, ColorConversionCodes.GRAY2BGR);
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
