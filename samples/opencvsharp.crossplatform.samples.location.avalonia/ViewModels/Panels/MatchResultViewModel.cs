using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp.CrossPlatform.Core.Profiling;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Mapping;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Profiling;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public sealed partial class MatchResultViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<MatchOverlayViewModel> matchOverlays = [];

    [ObservableProperty]
    private string bestScoreText = "-";

    [ObservableProperty]
    private string bestPointText = "-";

    [ObservableProperty]
    private string matchCountText = "-";

    [ObservableProperty]
    private bool matchCountBadgeVisible;

    [ObservableProperty]
    private string profileDisplayText = "";

    [ObservableProperty]
    private IReadOnlyList<ProfileStepViewModel> profileSteps = [];

    [ObservableProperty]
    private string profileTotalText = "";

    [ObservableProperty]
    private bool profileVisible;

    public MatchResultApplyOutcome Apply(
        TemplateLocatorResult result,
        TemplateLocatorViewModel algorithm,
        TemplateMatchMethodViewModel method)
    {
        MatchOverlays = MatchResultMapper.CreateOverlays(result);
        BestScoreText = result.BestScore.ToString("0.0000");
        BestPointText = $"({result.BestLocation.X}, {result.BestLocation.Y})";
        MatchCountText = result.Matches.Count.ToString();
        MatchCountBadgeVisible = result.Matches.Count > 0;

        if (result.Profile is not null)
        {
            ProfileDisplayText = result.Profile.ToDisplayText();
            ProfileSteps = result.Profile.ToStepViewModels();
            ProfileTotalText = $"总耗时 {result.Profile.TotalMs:0.000} ms";
            ProfileVisible = true;
            return new MatchResultApplyOutcome(result.Profile.ToStatusText(), result.Profile);
        }

        ProfileDisplayText = "";
        ProfileSteps = [];
        ProfileTotalText = "";
        ProfileVisible = false;
        var methodText = algorithm.UsesTemplateMatchMethod ? $"，方法：{method.Name}" : "";
        return new MatchResultApplyOutcome(
            $"{algorithm.Locator.Name}{methodText}。耗时 {result.Elapsed.TotalMilliseconds:0.0} 毫秒。",
            null);
    }

    public void Clear()
    {
        MatchOverlays = [];
        BestScoreText = "-";
        BestPointText = "-";
        MatchCountText = "-";
        MatchCountBadgeVisible = false;
        ProfileDisplayText = "";
        ProfileSteps = [];
        ProfileTotalText = "";
        ProfileVisible = false;
    }
}

public sealed record MatchResultApplyOutcome(string StatusText, ProfileResult? ProfileToLog);
