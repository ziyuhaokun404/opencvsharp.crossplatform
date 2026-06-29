using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core.Profiling;

namespace OpenCvSharp.CrossPlatform.Core.Matching;

public sealed record TemplateLocatorOptions(
    TemplateMatchModes Method,
    bool HigherIsBetter,
    double Threshold,
    bool UseGrayscale,
    double NmsOverlapThreshold,
    int MaxRefineCandidates = 0,
    int MaxMatches = 0,
    bool PreserveUnrefinedMatches = true);

public sealed record TemplateLocatorResult(
    Point BestLocation,
    double BestScore,
    Size TemplateSize,
    IReadOnlyList<MatchCandidate> Candidates,
    IReadOnlyList<MatchCandidate> Matches,
    TimeSpan Elapsed,
    ProfileResult? Profile = null,
    double? PyramidScale = null,
    Size? PyramidSourceSize = null,
    Size? PyramidTemplateSize = null,
    Size? PyramidWorkSize = null,
    Size? PyramidWorkTemplateSize = null,
    Size? PyramidResultSize = null,
    int? PyramidThresholdPixels = null,
    int? PyramidMinTemplateEdge = null);

public readonly record struct MatchCandidate(Rect Rect, double Score, double Angle = 0);

public interface ITemplateLocator
{
    string Name { get; }

    TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options);
}
