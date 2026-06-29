using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators.BuiltIn;

/// <summary>
/// 旋转算子。
/// </summary>
public sealed class RotateOperator : IImageOperator
{
    private const string AngleKey = "angle";
    private const string BorderModeKey = "borderMode";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "rotate",
        "Rotate",
        "几何",
        "围绕图像中心旋转。",
        [
            OperatorParameter.Numeric(AngleKey, "角度", 18, -180, 180),
            OperatorParameter.Choice(
                BorderModeKey,
                "边界模式",
                1,
                [
                    new OperatorParameterOption(0, "常量填充"),
                    new OperatorParameterOption(1, "镜像边界")
                ])
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(AngleKey, 18);
        var paramB = parameters.GetValueOrDefault(BorderModeKey, 1);
        var angle = Math.Clamp(paramA, -180, 180);
        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(source.Width / 2f, source.Height / 2f), angle, 1);
        var result = new Mat();
        var borderMode = paramB == 0 ? BorderTypes.Constant : BorderTypes.Reflect101;
        Cv2.WarpAffine(source, result, rotation, source.Size(), InterpolationFlags.Linear, borderMode, Scalar.All(0));
        return result;
    }
}
