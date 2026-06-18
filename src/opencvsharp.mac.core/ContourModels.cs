using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace OpenCvSharp.Mac.Core;

public sealed record TemplateContourProfile(Size TemplateSize, IReadOnlyList<ContourDescriptor> Contours);

public sealed record ContourExtractionSettings(
    double MinimumContourArea,
    double CannyLowThreshold,
    double CannyHighThreshold,
    double GradientThresholdScale,
    int CloseIterations,
    int MaximumTemplateContours,
    double MinimumTemplateContourCoverage,
    double EdgeDistanceTolerance);

public sealed record ContourTrainingResult(
    ContourExtractionSettings Settings,
    int TemplateContourCount,
    int CandidateCount,
    int MatchCount,
    double BestScore,
    double SuggestedThreshold,
    TimeSpan Elapsed);

internal sealed record ContourTrainingEvaluation(
    ContourExtractionSettings? Settings,
    int TemplateContourCount,
    int CandidateCount,
    int MatchCount,
    double BestScore,
    double TrainingScore,
    double SuggestedThreshold)
{
    public static ContourTrainingEvaluation Empty { get; } = new(null, 0, 0, 0, 0, double.NegativeInfinity, 0);
}

public sealed record ContourDescriptor(
    Point[] Contour,
    Rect Bounds,
    double Area,
    double AspectRatio,
    double Extent)
{
    public static ContourDescriptor Create(Point[] contour)
    {
        var bounds = Cv2.BoundingRect(contour);
        var area = Cv2.ContourArea(contour);
        var boundsArea = bounds.Width * bounds.Height;
        return new ContourDescriptor(
            contour,
            bounds,
            area,
            bounds.Height == 0 ? 0 : (double)bounds.Width / bounds.Height,
            boundsArea == 0 ? 0 : area / boundsArea);
    }
}
