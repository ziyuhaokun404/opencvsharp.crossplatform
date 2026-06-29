using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Presentation.Mapping;

internal static class MatchResultMapper
{
    public static IReadOnlyList<MatchOverlayViewModel> CreateOverlays(TemplateLocatorResult result)
    {
        if (result.Candidates.Count == 0)
            return [];

        var bestRect = new CvRect(
            result.BestLocation.X,
            result.BestLocation.Y,
            result.TemplateSize.Width,
            result.TemplateSize.Height);
        var overlays = result.Matches
            .Select(match => new MatchOverlayViewModel(
                match.Rect.X,
                match.Rect.Y,
                match.Rect.Width,
                match.Rect.Height,
                match.Angle,
                match.Rect == bestRect))
            .ToList();

        if (overlays.All(overlay => !overlay.IsBestMatch))
        {
            overlays.Insert(0, new MatchOverlayViewModel(
                bestRect.X,
                bestRect.Y,
                bestRect.Width,
                bestRect.Height,
                0,
                true));
        }

        return overlays;
    }

    public static PyramidInfoState CreatePyramidInfo(TemplateLocatorResult result)
    {
        if (result.PyramidScale is null)
        {
            return new PyramidInfoState(
                "当前算法不使用金字塔。",
                true,
                "不使用",
                "-",
                FormatSize(result.PyramidSourceSize),
                FormatSize(result.PyramidTemplateSize),
                FormatSize(result.PyramidResultSize),
                "-",
                "-");
        }

        var source = FormatSize(result.PyramidSourceSize);
        var template = FormatSize(result.PyramidTemplateSize);
        var workSource = FormatSize(result.PyramidWorkSize);
        var workTemplate = FormatSize(result.PyramidWorkTemplateSize);
        var resultSize = FormatSize(result.PyramidResultSize);
        var threshold = result.PyramidThresholdPixels?.ToString() ?? "-";
        var minEdge = result.PyramidMinTemplateEdge?.ToString() ?? "-";
        var enabled = result.PyramidScale < 1.0;

        return new PyramidInfoState(
            enabled
                ? "源图和模板已缩放后匹配，结果坐标映射回原图。"
                : "当前尺寸未超过金字塔阈值，直接在原图尺度匹配。",
            true,
            enabled ? "已启用" : "未启用",
            enabled ? $"×{result.PyramidScale.Value:0.000}" : "×1.000",
            enabled ? $"{source} → {workSource}" : source,
            enabled ? $"{template} → {workTemplate}" : template,
            resultSize,
            threshold,
            minEdge);
    }

    private static string FormatSize(CvSize? size)
        => size is { } value ? $"{value.Width}×{value.Height}" : "-";
}
