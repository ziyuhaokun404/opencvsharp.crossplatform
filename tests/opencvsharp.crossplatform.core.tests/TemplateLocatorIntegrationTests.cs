using System;
using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core.Matching;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class TemplateLocatorIntegrationTests
{
    private static TemplateLocatorOptions CreateMatchOptions(double threshold = 0.55)
        => new(
            TemplateMatchModes.CCoeffNormed,
            HigherIsBetter: true,
            Threshold: threshold,
            UseGrayscale: true,
            NmsOverlapThreshold: 0.3);

    private static (Mat Source, Mat Template, Point ExpectedLocation) CreateEmbeddedPattern(
        int sourceWidth = 240,
        int sourceHeight = 180,
        int templateWidth = 48,
        int templateHeight = 36,
        int x = 72,
        int y = 54,
        byte background = 25,
        byte foreground = 220)
    {
        var source = new Mat(sourceHeight, sourceWidth, MatType.CV_8UC1, new Scalar(background));
        var template = new Mat(templateHeight, templateWidth, MatType.CV_8UC1, new Scalar(foreground));
        Cv2.Rectangle(
            template,
            new Rect(4, 4, templateWidth - 8, templateHeight - 8),
            new Scalar(foreground - 35),
            -1);

        var expectedLocation = new Point(x, y);
        template.CopyTo(new Mat(source, new Rect(expectedLocation, template.Size())));
        return (source, template, expectedLocation);
    }

    [Fact]
    public void MatchTemplateLocator_Locate_FindsEmbeddedPattern()
    {
        var (source, template, expectedLocation) = CreateEmbeddedPattern();
        using (source)
        using (template)
        {
            using var locator = new MatchTemplateLocator();

            var result = locator.Locate(source, template, CreateMatchOptions());

            Assert.NotEmpty(result.Matches);
            Assert.Equal(expectedLocation, result.BestLocation);
            Assert.True(result.BestScore >= 0.55);
        }
    }

    [Fact]
    public void ContourTemplateLocator_TrainThenLocate_FindsEmbeddedPattern()
    {
        var (source, template, expectedLocation) = CreateEmbeddedPattern(
            sourceWidth: 320,
            sourceHeight: 240,
            templateWidth: 64,
            templateHeight: 48,
            x: 96,
            y: 72);
        using (source)
        using (template)
        {
            var locator = new ContourTemplateLocator();
            var training = locator.Train(source, template, nmsOverlapThreshold: 0.3);
            Assert.True(training.TemplateContourCount > 0);
            Assert.True(training.SuggestedThreshold > 0);

            var result = locator.Locate(
                source,
                template,
                CreateMatchOptions(training.SuggestedThreshold));

            Assert.NotEmpty(result.Matches);
            Assert.InRange(result.BestLocation.X, expectedLocation.X - 4, expectedLocation.X + 4);
            Assert.InRange(result.BestLocation.Y, expectedLocation.Y - 4, expectedLocation.Y + 4);
        }
    }

    [Fact]
    public void MatchTemplateLocator_Locate_RejectsTemplateLargerThanSource()
    {
        using var source = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(30));
        using var template = new Mat(60, 60, MatType.CV_8UC1, Scalar.All(200));
        using var locator = new MatchTemplateLocator();

        Assert.Throws<InvalidOperationException>(() =>
            locator.Locate(source, template, CreateMatchOptions()));
    }
}
