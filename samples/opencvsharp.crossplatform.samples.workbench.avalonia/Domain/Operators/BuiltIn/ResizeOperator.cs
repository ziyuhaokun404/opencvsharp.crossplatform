using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

/// <summary>
/// 缩放算子。
/// </summary>
public sealed class ResizeOperator : IImageOperator
{
    private const string ScaleKey = "scalePercent";
    private const string InterpolationKey = "interpolation";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "resize",
        "Resize",
        "几何",
        "按目标比例重采样图像。",
        [
            OperatorParameter.Numeric(ScaleKey, "缩放 %", 75, 10, 200),
            OperatorParameter.Choice(
                InterpolationKey,
                "插值方式",
                1,
                [
                    new OperatorParameterOption(0, "最近邻"),
                    new OperatorParameterOption(1, "线性"),
                    new OperatorParameterOption(2, "三次")
                ])
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(ScaleKey, 75);
        var paramB = parameters.GetValueOrDefault(InterpolationKey, 1);
        var scale = Math.Clamp(paramA, 10, 200) / 100.0;
        var interpolation = paramB switch
        {
            0 => InterpolationFlags.Nearest,
            2 => InterpolationFlags.Cubic,
            _ => InterpolationFlags.Linear
        };

        var result = new Mat();
        Cv2.Resize(source, result, new Size(), scale, scale, interpolation);
        return result;
    }
}
