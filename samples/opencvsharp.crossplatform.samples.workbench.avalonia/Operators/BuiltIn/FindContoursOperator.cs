using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators.BuiltIn;

/// <summary>
/// 轮廓检测算子。
/// </summary>
public sealed class FindContoursOperator : IImageOperator
{
    private const string ThresholdKey = "threshold";
    private const string MinAreaKey = "minArea";

    public ImageOperatorDescriptor Descriptor { get; } = new(
        "find-contours",
        "Find Contours",
        "轮廓",
        "检测连通轮廓并标注边界框。",
        [
            OperatorParameter.Numeric(ThresholdKey, "阈值", 128, 0, 255),
            OperatorParameter.Numeric(MinAreaKey, "最小面积", 900, 10, 5000)
        ],
        false);

    public Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context)
    {
        var paramA = parameters.GetValueOrDefault(ThresholdKey, 128);
        var paramB = parameters.GetValueOrDefault(MinAreaKey, 900);
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, paramA, 255, ThresholdTypes.Binary);
        Cv2.FindContours(thresholded, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var result = source.Clone();
        var minArea = Math.Max(10, paramB);
        foreach (var contour in contours.Where(contour => Cv2.ContourArea(contour) >= minArea))
        {
            var rect = Cv2.BoundingRect(contour);
            Cv2.Rectangle(result, rect, new Scalar(80, 190, 120), 4, LineTypes.AntiAlias);
        }

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
