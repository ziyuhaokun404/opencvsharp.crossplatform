using OpenCvSharp.CrossPlatform.Core.Profiling;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Profiling;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class ProfilePresentationTests
{
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
        var steps = new[] { new ProfileStep("a", 0.0) };
        var result = new ProfileResult("op", 0.0, steps);

        var vm = Assert.Single(result.ToStepViewModels());

        Assert.Equal(0, vm.Percentage);
        Assert.Equal(0, vm.BarFraction);
    }
}
