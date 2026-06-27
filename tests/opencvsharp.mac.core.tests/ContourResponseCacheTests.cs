using OpenCvSharp;
using OpenCvSharp.Mac.Core;
using Xunit;

namespace OpenCvSharp.Mac.Core.Tests;

public class ContourResponseCacheTests
{
    private static ContourExtractionSettings Settings(
        double minArea = 10, double cannyLow = 50, double cannyHigh = 150,
        double gradScale = 0.4, int closeIter = 1, int maxContours = 8,
        double minCoverage = 0.5, double edgeTolerance = 2.0)
        => new(minArea, cannyLow, cannyHigh, gradScale, closeIter, maxContours, minCoverage, edgeTolerance);

    [Fact]
    public void Matches_ReturnsTrue_ForEqualSettings()
    {
        var s = Settings();
        var cache = ContourResponseCache.Empty(s, new Size(20, 20), 3);

        Assert.True(cache.Matches(Settings()));
        cache.Dispose();
    }

    [Fact]
    public void Matches_ReturnsFalse_ForDifferentSettings()
    {
        var s = Settings(minArea: 10);
        var cache = ContourResponseCache.Empty(s, new Size(20, 20), 3);

        Assert.False(cache.Matches(Settings(minArea: 20)));
        cache.Dispose();
    }

    [Fact]
    public void Empty_CreatesCacheWithZeroEdgeCount()
    {
        var cache = ContourResponseCache.Empty(Settings(), new Size(15, 15), 2);

        Assert.Equal(0, cache.EdgeCount);
        Assert.Equal(2, cache.TemplateContourCount);
        Assert.Equal(new Size(15, 15), cache.TemplateSize);
        cache.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutThrowing()
    {
        var cache = ContourResponseCache.Empty(Settings(), new Size(10, 10), 1);

        cache.Dispose();
        cache.Dispose();
    }
}
