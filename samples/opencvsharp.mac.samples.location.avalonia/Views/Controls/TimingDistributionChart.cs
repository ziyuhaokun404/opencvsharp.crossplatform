using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia.Controls;

public sealed class TimingDistributionChart : Control
{
    public static readonly StyledProperty<BenchmarkSummary?> SummaryProperty =
        AvaloniaProperty.Register<TimingDistributionChart, BenchmarkSummary?>(nameof(Summary));

    private static readonly Typeface ChartTypeface = new("Inter");
    private static readonly IBrush AxisBrush = new SolidColorBrush(Color.Parse("#475467"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));
    private static readonly IBrush BarBrush = new SolidColorBrush(Color.Parse("#60A5FA"));
    private static readonly IBrush BarStrokeBrush = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush AverageBrush = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush P50Brush = new SolidColorBrush(Color.Parse("#059669"));
    private static readonly IBrush P95Brush = new SolidColorBrush(Color.Parse("#DC2626"));
    private static readonly Pen AxisPen = new(AxisBrush, 1);
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen BarPen = new(BarStrokeBrush, 1);
    private static readonly Pen AveragePen = new(AverageBrush, 1.5, DashStyle.Dash);
    private static readonly Pen P50Pen = new(P50Brush, 1.5, DashStyle.Dash);
    private static readonly Pen P95Pen = new(P95Brush, 1.5, DashStyle.Dash);

    public BenchmarkSummary? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    static TimingDistributionChart()
    {
        AffectsRender<TimingDistributionChart>(SummaryProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(Brushes.White, bounds);

        var summary = Summary;
        if (summary is null || summary.RunTimingsMs.Count == 0)
            return;

        var values = summary.RunTimingsMs;
        var (min, max) = GetDisplayRange(summary);
        var plot = new AvaloniaRect(58, 18, Math.Max(1, bounds.Width - 86), Math.Max(1, bounds.Height - 62));
        var bins = BuildBins(values, min, max);
        var maxCount = Math.Max(1, bins.Max());

        DrawGrid(context, plot, maxCount);
        DrawBars(context, plot, bins, maxCount);
        DrawReferenceLine(context, plot, min, max, summary.AverageMs, "平均", AverageBrush, AveragePen, 0);
        DrawReferenceLine(context, plot, min, max, summary.P50Ms, "P50", P50Brush, P50Pen, 1);
        DrawReferenceLine(context, plot, min, max, summary.P95Ms, "P95", P95Brush, P95Pen, 2);
        DrawAxes(context, plot, min, max);
        DrawLegend(context, plot);
    }

    private static (double Min, double Max) GetDisplayRange(BenchmarkSummary summary)
    {
        var min = summary.MinMs;
        var max = summary.MaxMs;
        if (Math.Abs(max - min) >= 0.001)
            return (min, max);

        var padding = Math.Max(0.001, Math.Abs(summary.AverageMs) * 0.05);
        return (Math.Max(0, min - padding), max + padding);
    }

    private static int[] BuildBins(IReadOnlyList<double> values, double min, double max)
    {
        var binCount = Math.Clamp((int)Math.Ceiling(Math.Sqrt(values.Count)), 6, 18);
        var bins = new int[binCount];
        var range = max - min;

        foreach (var value in values)
        {
            var index = range <= 0
                ? 0
                : (int)Math.Floor((value - min) / range * binCount);
            index = Math.Clamp(index, 0, binCount - 1);
            bins[index]++;
        }

        return bins;
    }

    private static void DrawGrid(DrawingContext context, AvaloniaRect plot, int maxCount)
    {
        const int lines = 4;
        for (var i = 0; i <= lines; i++)
        {
            var fraction = i / (double)lines;
            var y = plot.Bottom - plot.Height * fraction;
            context.DrawLine(GridPen, new AvaloniaPoint(plot.Left, y), new AvaloniaPoint(plot.Right, y));

            var value = (int)Math.Round(maxCount * fraction);
            DrawLabel(context, value.ToString(), new AvaloniaPoint(12, y - 8), 11, AxisBrush);
        }
    }

    private static void DrawBars(DrawingContext context, AvaloniaRect plot, IReadOnlyList<int> bins, int maxCount)
    {
        var slotWidth = plot.Width / bins.Count;
        var barGap = Math.Clamp(slotWidth * 0.18, 2.0, 8.0);

        for (var i = 0; i < bins.Count; i++)
        {
            var height = bins[i] <= 0 ? 0 : Math.Max(2, plot.Height * bins[i] / maxCount);
            var rect = new AvaloniaRect(
                plot.Left + i * slotWidth + barGap / 2,
                plot.Bottom - height,
                Math.Max(1, slotWidth - barGap),
                height);
            context.DrawRectangle(BarBrush, BarPen, rect, 3);
        }
    }

    private static void DrawReferenceLine(
        DrawingContext context,
        AvaloniaRect plot,
        double min,
        double max,
        double value,
        string label,
        IBrush brush,
        Pen pen,
        int labelIndex)
    {
        var x = MapX(plot, value, min, max);
        context.DrawLine(pen, new AvaloniaPoint(x, plot.Top), new AvaloniaPoint(x, plot.Bottom));
        DrawLabel(context, $"{label} {value:0.000}", new AvaloniaPoint(x + 4, plot.Top + 2 + labelIndex * 15), 10, brush);
    }

    private static void DrawAxes(DrawingContext context, AvaloniaRect plot, double min, double max)
    {
        context.DrawLine(AxisPen, new AvaloniaPoint(plot.Left, plot.Top), new AvaloniaPoint(plot.Left, plot.Bottom));
        context.DrawLine(AxisPen, new AvaloniaPoint(plot.Left, plot.Bottom), new AvaloniaPoint(plot.Right, plot.Bottom));

        foreach (var fraction in new[] { 0.0, 0.5, 1.0 })
        {
            var x = plot.Left + plot.Width * fraction;
            var value = min + (max - min) * fraction;
            context.DrawLine(AxisPen, new AvaloniaPoint(x, plot.Bottom), new AvaloniaPoint(x, plot.Bottom + 5));
            DrawLabel(context, $"{value:0.000} ms", new AvaloniaPoint(x - 26, plot.Bottom + 10), 11, AxisBrush);
        }
    }

    private static void DrawLegend(DrawingContext context, AvaloniaRect plot)
    {
        DrawLegendItem(context, new AvaloniaPoint(plot.Right - 168, plot.Top - 2), "平均", AverageBrush);
        DrawLegendItem(context, new AvaloniaPoint(plot.Right - 112, plot.Top - 2), "P50", P50Brush);
        DrawLegendItem(context, new AvaloniaPoint(plot.Right - 58, plot.Top - 2), "P95", P95Brush);
    }

    private static void DrawLegendItem(DrawingContext context, AvaloniaPoint origin, string label, IBrush brush)
    {
        context.DrawRectangle(brush, null, new AvaloniaRect(origin.X, origin.Y + 4, 10, 3), 1.5);
        DrawLabel(context, label, new AvaloniaPoint(origin.X + 14, origin.Y), 10, brush);
    }

    private static double MapX(AvaloniaRect plot, double value, double min, double max)
    {
        var fraction = (value - min) / (max - min);
        return plot.Left + Math.Clamp(fraction, 0, 1) * plot.Width;
    }

    private static void DrawLabel(DrawingContext context, string text, AvaloniaPoint origin, double size, IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            ChartTypeface,
            size,
            brush);
        context.DrawText(formatted, origin);
    }
}
