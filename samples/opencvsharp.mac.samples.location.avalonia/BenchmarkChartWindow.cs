using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia;

public sealed class BenchmarkChartWindow : AvaloniaWindow
{
    public BenchmarkChartWindow(BenchmarkSummary summary)
    {
        Title = "稳定性耗时折线图";
        Width = 920;
        Height = 560;
        MinWidth = 720;
        MinHeight = 460;
        Background = new SolidColorBrush(Color.Parse("#EDF1F5"));

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18),
            RowSpacing = 12
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12
        };
        header.Children.Add(new TextBlock
        {
            Text = "100 次运行耗时",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#111827")),
            VerticalAlignment = VerticalAlignment.Center
        });
        var summaryText = new TextBlock
        {
            Text = $"平均 {summary.AverageMs:0.000} ms  P95 {summary.P95Ms:0.000} ms  标准差 {summary.StandardDeviationMs:0.000} ms",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#475467")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(summaryText, 1);
        header.Children.Add(summaryText);
        root.Children.Add(header);

        var chart = new BenchmarkLineChart
        {
            Summary = summary
        };
        Grid.SetRow(chart, 1);
        root.Children.Add(chart);

        var footer = new TextBlock
        {
            Text = $"最小 {summary.MinMs:0.000} ms，最大 {summary.MaxMs:0.000} ms，P50 {summary.P50Ms:0.000} ms。横轴为运行序号，纵轴为单次耗时。",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#475467"))
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }
}

public sealed class BenchmarkLineChart : Control
{
    public static readonly StyledProperty<BenchmarkSummary?> SummaryProperty =
        AvaloniaProperty.Register<BenchmarkLineChart, BenchmarkSummary?>(nameof(Summary));

    private static readonly Typeface LabelTypeface = new("Inter");
    private static readonly IBrush AxisBrush = new SolidColorBrush(Color.Parse("#475467"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));
    private static readonly IBrush LineBrush = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush PointBrush = new SolidColorBrush(Color.Parse("#1D4ED8"));
    private static readonly IBrush AverageBrush = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly Pen AxisPen = new(AxisBrush, 1);
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen LinePen = new(LineBrush, 2);
    private static readonly Pen AveragePen = new(AverageBrush, 1.5, DashStyle.Dash);

    public BenchmarkSummary? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    static BenchmarkLineChart()
    {
        AffectsRender<BenchmarkLineChart>(SummaryProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(Brushes.White, bounds);

        var summary = Summary;
        if (summary is null || summary.RunTimingsMs.Count == 0)
            return;

        var plot = new AvaloniaRect(64, 24, Math.Max(1, bounds.Width - 88), Math.Max(1, bounds.Height - 76));
        var values = summary.RunTimingsMs;
        var min = Math.Min(summary.MinMs, values.Count > 0 ? values.Min() : summary.MinMs);
        var max = Math.Max(summary.MaxMs, values.Count > 0 ? values.Max() : summary.MaxMs);
        if (Math.Abs(max - min) < 0.001)
        {
            min = Math.Max(0, min - 0.5);
            max += 0.5;
        }

        DrawGrid(context, plot, min, max);
        DrawAverageLine(context, plot, min, max, summary.AverageMs);
        DrawSeries(context, plot, values, min, max);
        DrawAxes(context, plot, values.Count);
    }

    private static void DrawGrid(DrawingContext context, AvaloniaRect plot, double min, double max)
    {
        const int horizontalLines = 5;
        for (var i = 0; i <= horizontalLines; i++)
        {
            var fraction = i / (double)horizontalLines;
            var y = plot.Bottom - plot.Height * fraction;
            context.DrawLine(GridPen, new AvaloniaPoint(plot.Left, y), new AvaloniaPoint(plot.Right, y));

            var value = min + (max - min) * fraction;
            DrawLabel(context, $"{value:0.000}", new AvaloniaPoint(8, y - 8), 11, AxisBrush);
        }
    }

    private static void DrawAverageLine(DrawingContext context, AvaloniaRect plot, double min, double max, double average)
    {
        var y = MapY(plot, average, min, max);
        context.DrawLine(AveragePen, new AvaloniaPoint(plot.Left, y), new AvaloniaPoint(plot.Right, y));
        DrawLabel(context, $"平均 {average:0.000} ms", new AvaloniaPoint(plot.Right - 110, y - 20), 11, AverageBrush);
    }

    private static void DrawSeries(DrawingContext context, AvaloniaRect plot, IReadOnlyList<double> values, double min, double max)
    {
        if (values.Count == 1)
        {
            var point = new AvaloniaPoint(plot.Left, MapY(plot, values[0], min, max));
            context.DrawEllipse(PointBrush, null, point, 3.5, 3.5);
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var point = MapPoint(plot, i, values.Count, values[i], min, max);
                if (i == 0)
                    ctx.BeginFigure(point, false);
                else
                    ctx.LineTo(point);
            }
        }

        context.DrawGeometry(null, LinePen, geometry);

        for (var i = 0; i < values.Count; i += 10)
            context.DrawEllipse(PointBrush, null, MapPoint(plot, i, values.Count, values[i], min, max), 2.5, 2.5);
        context.DrawEllipse(PointBrush, null, MapPoint(plot, values.Count - 1, values.Count, values[^1], min, max), 2.5, 2.5);
    }

    private static void DrawAxes(DrawingContext context, AvaloniaRect plot, int count)
    {
        context.DrawLine(AxisPen, new AvaloniaPoint(plot.Left, plot.Top), new AvaloniaPoint(plot.Left, plot.Bottom));
        context.DrawLine(AxisPen, new AvaloniaPoint(plot.Left, plot.Bottom), new AvaloniaPoint(plot.Right, plot.Bottom));

        foreach (var index in new[] { 1, 25, 50, 75, count })
        {
            if (index < 1 || index > count)
                continue;

            var x = plot.Left + (index - 1) * plot.Width / Math.Max(1, count - 1);
            context.DrawLine(AxisPen, new AvaloniaPoint(x, plot.Bottom), new AvaloniaPoint(x, plot.Bottom + 5));
            DrawLabel(context, index.ToString(), new AvaloniaPoint(x - 8, plot.Bottom + 10), 11, AxisBrush);
        }
    }

    private static AvaloniaPoint MapPoint(AvaloniaRect plot, int index, int count, double value, double min, double max)
    {
        var x = plot.Left + index * plot.Width / Math.Max(1, count - 1);
        return new AvaloniaPoint(x, MapY(plot, value, min, max));
    }

    private static double MapY(AvaloniaRect plot, double value, double min, double max)
    {
        var fraction = (value - min) / (max - min);
        return plot.Bottom - Math.Clamp(fraction, 0, 1) * plot.Height;
    }

    private static void DrawLabel(DrawingContext context, string text, AvaloniaPoint origin, double size, IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            size,
            brush);
        context.DrawText(formatted, origin);
    }
}
