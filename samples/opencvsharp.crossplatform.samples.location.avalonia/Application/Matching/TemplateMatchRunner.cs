using System;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;

internal static class TemplateMatchRunner
{
    public static TemplateLocatorResult Locate(
        ITemplateLocator locator,
        TemplateImageSession session,
        TemplateLocatorOptions options)
    {
        var source = session.GetSourceMat();
        var template = session.GetTemplateMat();
        EnsureTemplateFits(source, template);
        return locator.Locate(source, template, options);
    }

    public static ContourTrainingResult Train(
        ContourTemplateLocator locator,
        byte[] sourceBytes,
        byte[] templateBytes,
        double nmsOverlapThreshold)
    {
        using var source = OpenCvImageCodec.Decode(sourceBytes);
        using var template = OpenCvImageCodec.Decode(templateBytes);
        return locator.Train(source, template, nmsOverlapThreshold);
    }

    public static TemplateLocatorResult Locate(
        ITemplateLocator locator,
        Mat source,
        Mat template,
        TemplateLocatorOptions options)
    {
        EnsureTemplateFits(source, template);
        return locator.Locate(source, template, options);
    }

    public static void EnsureTemplateFits(Mat source, Mat template)
    {
        if (template.Width > source.Width || template.Height > source.Height)
            throw new InvalidOperationException("模板图必须小于源图。");
    }
}
