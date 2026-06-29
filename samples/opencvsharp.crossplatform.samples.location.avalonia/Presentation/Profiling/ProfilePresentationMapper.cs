using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenCvSharp.CrossPlatform.Core.Profiling;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Profiling;

public static class ProfilePresentationMapper
{
    public static string ToDisplayText(this ProfileResult profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"── {profile.OperationName}  总耗时 {profile.TotalMs:0.000} ms ──");
        foreach (var step in profile.Steps)
            sb.AppendLine(FormatStepLine(step));
        return sb.ToString();
    }

    public static string ToStatusText(this ProfileResult profile)
    {
        var sb = new StringBuilder();
        sb.Append($"{profile.OperationName}: ");
        for (var i = 0; i < profile.Steps.Length; i++)
        {
            if (i > 0)
                sb.Append(" → ");
            sb.Append($"{profile.Steps[i].Name} {profile.Steps[i].ElapsedMs:0.00}ms");
        }

        sb.Append($" | 总计 {profile.TotalMs:0.00}ms");
        return sb.ToString();
    }

    public static IReadOnlyList<ProfileStepViewModel> ToStepViewModels(this ProfileResult profile)
    {
        var result = new List<ProfileStepViewModel>(profile.Steps.Length);
        foreach (var step in profile.Steps)
        {
            var pct = profile.TotalMs > 0 ? step.ElapsedMs / profile.TotalMs * 100.0 : 0;
            result.Add(new ProfileStepViewModel(
                step.Name,
                $"{step.ElapsedMs:0.000}",
                step.Detail,
                pct));
        }

        return result;
    }

    public static string FormatStepLine(ProfileStep step)
    {
        var ms = step.ElapsedMs.ToString("0.000", CultureInfo.InvariantCulture);
        return step.Detail is null
            ? $"  {step.Name,-28} {ms,8} ms"
            : $"  {step.Name,-28} {ms,8} ms  ({step.Detail})";
    }
}
