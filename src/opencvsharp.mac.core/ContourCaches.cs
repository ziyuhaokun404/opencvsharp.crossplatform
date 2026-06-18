using System;
using System.Runtime.CompilerServices;
using OpenCvSharp;

namespace OpenCvSharp.Mac.Core;

internal sealed class ContourPyramidCache : IDisposable
{
    private readonly int sourceObjectId;
    private readonly int templateObjectId;
    private readonly Size sourceSize;
    private readonly Size templateSize;
    private readonly MatType sourceType;
    private readonly MatType templateType;
    private readonly double scale;

    public ContourPyramidCache(Mat source, Mat template, double scale, Mat workSource, Mat workTemplate)
    {
        sourceObjectId = RuntimeHelpers.GetHashCode(source);
        templateObjectId = RuntimeHelpers.GetHashCode(template);
        sourceSize = source.Size();
        templateSize = template.Size();
        sourceType = source.Type();
        templateType = template.Type();
        this.scale = scale;
        WorkSource = workSource;
        WorkTemplate = workTemplate;
    }

    public Mat WorkSource { get; }

    public Mat WorkTemplate { get; }

    public ContourResponseCache? ResponseCache { get; set; }

    public bool Matches(Mat source, Mat template, double requestedScale)
    {
        return RuntimeHelpers.GetHashCode(source) == sourceObjectId &&
               RuntimeHelpers.GetHashCode(template) == templateObjectId &&
               source.Size() == sourceSize &&
               template.Size() == templateSize &&
               source.Type() == sourceType &&
               template.Type() == templateType &&
               Math.Abs(requestedScale - scale) < 0.000001;
    }

    public void Dispose()
    {
        ResponseCache?.Dispose();
        WorkSource.Dispose();
        WorkTemplate.Dispose();
    }
}

internal sealed class ContourResponseCache : IDisposable
{
    private readonly ContourExtractionSettings settings;

    public ContourResponseCache(
        ContourExtractionSettings settings,
        Size templateSize,
        int templateContourCount,
        int edgeCount,
        Mat distanceSum,
        Mat hitSum,
        Mat intensityResult)
    {
        this.settings = settings;
        TemplateSize = templateSize;
        TemplateContourCount = templateContourCount;
        EdgeCount = edgeCount;
        DistanceSum = distanceSum;
        HitSum = hitSum;
        IntensityResult = intensityResult;
    }

    public Size TemplateSize { get; }

    public int TemplateContourCount { get; }

    public int EdgeCount { get; }

    public Mat DistanceSum { get; }

    public Mat HitSum { get; }

    public Mat IntensityResult { get; }

    public bool Matches(ContourExtractionSettings requestedSettings) => requestedSettings == settings;

    public static ContourResponseCache Empty(ContourExtractionSettings settings, Size templateSize, int templateContourCount)
    {
        return new ContourResponseCache(
            settings,
            templateSize,
            templateContourCount,
            0,
            new Mat(1, 1, MatType.CV_32F, Scalar.Black),
            new Mat(1, 1, MatType.CV_32F, Scalar.Black),
            new Mat(1, 1, MatType.CV_32F, Scalar.Black));
    }

    public void Dispose()
    {
        DistanceSum.Dispose();
        HitSum.Dispose();
        IntensityResult.Dispose();
    }
}
