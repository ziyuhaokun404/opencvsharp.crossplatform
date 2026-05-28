using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace OpenCvSharp.TemplateMatching;

public sealed record TemplateLocatorOptions(
    TemplateMatchModes Method,
    bool HigherIsBetter,
    double Threshold,
    bool UseGrayscale,
    double NmsOverlapThreshold);

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

public readonly record struct MatchCandidate(Rect Rect, double Score);
