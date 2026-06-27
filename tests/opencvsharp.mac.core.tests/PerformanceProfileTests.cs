using System;
using System.Linq;
using OpenCvSharp.Mac.Core;
using Xunit;

namespace OpenCvSharp.Mac.Core.Tests;

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

    [Fact]
    public void ToDisplayText_ContainsOperationNameAndTotalMs()
    {
        var profile = new PerformanceProfile("match-template");
        profile.Step("grayscale");
        var result = profile.Finish();

        var text = result.ToDisplayText();

        Assert.Contains("match-template", text);
        Assert.Contains("grayscale", text);
    }

    [Fact]
    public void ToStatusText_JoinsStepsWithArrows()
    {
        var profile = new PerformanceProfile("op");
        profile.Step("a");
        profile.Step("b");

        var result = profile.Finish();
        var text = result.ToStatusText();

        Assert.Contains("→", text);
        Assert.Contains("a", text);
        Assert.Contains("b", text);
        Assert.Contains("op", text);
    }

    [Fact]
    public void ToStepViewModels_ComputesPercentageRelativeToTotal()
    {
        // 构造一个可控的 ProfileResult，避免依赖真实计时
        var steps = new[]
        {
            new ProfileStep("a", 10.0),
            new ProfileStep("b", 30.0),
            new ProfileStep("c", 60.0),
        };
        var result = new ProfileResult("op", 100.0, steps);

        var vms = result.ToStepViewModels();

        Assert.Equal(3, vms.Count);
        Assert.Equal(10.0, vms[0].Percentage);
        Assert.Equal(30.0, vms[1].Percentage);
        Assert.Equal(60.0, vms[2].Percentage);
    }

    [Fact]
    public void ProfileStepViewModel_BarFraction_ClampsToUnitInterval()
    {
        var vm = new ProfileStepViewModel("a", "1.0", null, 150.0);

        Assert.Equal(1.0, vm.BarFraction);
    }

    [Fact]
    public void ProfileStepViewModel_BarFraction_IsZeroForZeroTotal()
    {
        // 总耗时为 0 时，百分比应为 0，BarFraction 也为 0
        var steps = new[] { new ProfileStep("a", 0.0) };
        var result = new ProfileResult("op", 0.0, steps);

        var vm = Assert.Single(result.ToStepViewModels());

        Assert.Equal(0, vm.Percentage);
        Assert.Equal(0, vm.BarFraction);
    }
}
