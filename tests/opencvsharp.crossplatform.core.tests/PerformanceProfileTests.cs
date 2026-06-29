using System.Linq;
using OpenCvSharp.CrossPlatform.Core.Profiling;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class PerformanceProfileTests
{
    [Fact]
    public void Finish_ReturnsOperationNameAndZeroSteps_WhenNoStepRecorded()
    {
        var profile = new PerformanceProfile("test-op");
        // 允许极短延时
        var result = profile.Finish();

        Assert.Equal("test-op", result.OperationName);
        Assert.Empty(result.Steps);
        Assert.True(result.TotalMs >= 0);
    }

    [Fact]
    public void Step_RecordsNameAndNonNegativeElapsed()
    {
        var profile = new PerformanceProfile("op");
        profile.Step("phase-a");

        var result = profile.Finish();

        var step = Assert.Single(result.Steps);
        Assert.Equal("phase-a", step.Name);
        Assert.Null(step.Detail);
        Assert.True(step.ElapsedMs >= 0);
    }

    [Fact]
    public void Step_WithDetail_PreservesDetail()
    {
        var profile = new PerformanceProfile("op");
        profile.Step("phase-a", "extra info");

        var result = profile.Finish();

        var step = Assert.Single(result.Steps);
        Assert.Equal("extra info", step.Detail);
    }

    [Fact]
    public void Step_MultipleCalls_PreservesOrder()
    {
        var profile = new PerformanceProfile("op");
        profile.Step("first");
        profile.Step("second");
        profile.Step("third");

        var result = profile.Finish();

        Assert.Equal(new[] { "first", "second", "third" }, result.Steps.Select(s => s.Name).ToArray());
    }
}
