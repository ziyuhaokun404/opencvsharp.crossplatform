using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCvSharp.TemplateMatching;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

/// <summary>
/// 详细性能分析结果，包含 wall-clock 计时和 GC 分配指标。
/// </summary>
public sealed record DetailedBenchmarkResult(
    BenchmarkSummary TimingSummary,
    long TotalAllocatedBytes,
    long PerCallAllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    ProfileResult? LastProfile,
    IReadOnlyList<TemplateLocatorResult> Results);

/// <summary>
/// 性能分析窗口：展示 wall-clock、GC 分配、分步耗时和耗时分布。
/// </summary>
public sealed class BenchmarkDetailWindow : AvaloniaWindow
{
    private static readonly Typeface LabelTypeface = new("Inter");
    private static readonly IBrush TextPrimary = new SolidColorBrush(Color.Parse("#111827"));
    private static readonly IBrush TextSecondary = new SolidColorBrush(Color.Parse("#475467"));
    private static readonly IBrush TextMuted = new SolidColorBrush(Color.Parse("#9CA3AF"));
    private static readonly IBrush AccentBlue = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush AccentGreen = new SolidColorBrush(Color.Parse("#059669"));
    private static readonly IBrush AccentAmber = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush CardBackground = Brushes.White;
    private static readonly IBrush CardBorder = new SolidColorBrush(Color.Parse("#E5E7EB"));
    private static readonly IBrush BarTrack = new SolidColorBrush(Color.Parse("#F1F5F9"));
    private static readonly IBrush BarFill = new SolidColorBrush(Color.Parse("#3B82F6"));

    public BenchmarkDetailWindow(DetailedBenchmarkResult result)
    {
        Title = "性能分析报告";
        Width = 1060;
        Height = 740;
        MinWidth = 900;
        MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#EDF1F5"));

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(20),
            RowSpacing = 14
        };

        // 标题
        root.Children.Add(CreateHeader(result));

        // 指标卡片行
        var metricsRow = CreateMetricsRow(result);
        Grid.SetRow(metricsRow, 1);
        root.Children.Add(metricsRow);

        // 分步耗时
        var profilePanel = CreateProfilePanel(result.LastProfile);
        Grid.SetRow(profilePanel, 2);
        root.Children.Add(profilePanel);

        // 耗时分布图
        var chart = CreateChartPanel(result.TimingSummary);
        Grid.SetRow(chart, 3);
        root.Children.Add(chart);

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root
        };
    }

    /// <summary>
    /// 在后台线程运行详细性能分析。
    /// </summary>
    public static DetailedBenchmarkResult RunDetailedBenchmark(
        ITemplateLocator locator,
        OpenCvSharp.Mat source,
        OpenCvSharp.Mat template,
        TemplateLocatorOptions options,
        int warmupRuns,
        int benchmarkRuns)
    {
        // Warmup
        for (var i = 0; i < warmupRuns; i++)
            locator.Locate(source, template, options);

        // 强制 GC，清理 warmup 产生的垃圾
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        var results = new List<TemplateLocatorResult>(benchmarkRuns);
        for (var i = 0; i < benchmarkRuns; i++)
            results.Add(locator.Locate(source, template, options));

        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        var totalAllocated = allocAfter - allocBefore;
        var summary = BenchmarkSummary.Create(results);

        return new DetailedBenchmarkResult(
            summary,
            totalAllocated,
            benchmarkRuns > 0 ? totalAllocated / benchmarkRuns : 0,
            gen0After - gen0Before,
            gen1After - gen1Before,
            gen2After - gen2Before,
            results[^1].Profile,
            results);
    }

    private static Control CreateHeader(DetailedBenchmarkResult result)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12
        };

        header.Children.Add(new TextBlock
        {
            Text = "性能分析报告",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EEF2FF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C7D2FE")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 3),
            Child = new TextBlock
            {
                Text = $"{result.Results.Count} 次运行",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = AccentBlue
            }
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        return header;
    }

    private static Control CreateMetricsRow(DetailedBenchmarkResult result)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 14
        };

        // 左：耗时统计
        var timingCard = CreateCard("耗时统计", CreateTimingMetrics(result.TimingSummary));
        grid.Children.Add(timingCard);

        // 右：GC / 内存
        var gcCard = CreateCard("GC / 内存", CreateGcMetrics(result));
        Grid.SetColumn(gcCard, 1);
        grid.Children.Add(gcCard);

        return grid;
    }

    private static Control CreateTimingMetrics(BenchmarkSummary s)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 8
        };

        AddMetricCell(grid, 0, 0, "平均", $"{s.AverageMs:0.000} ms");
        AddMetricCell(grid, 1, 0, "P50", $"{s.P50Ms:0.000} ms");
        AddMetricCell(grid, 2, 0, "P95", $"{s.P95Ms:0.000} ms");
        AddMetricCell(grid, 3, 0, "P99", Percentile(s.RunTimingsMs, 0.99));
        AddMetricCell(grid, 0, 1, "最小", $"{s.MinMs:0.000} ms");
        AddMetricCell(grid, 1, 1, "最大", $"{s.MaxMs:0.000} ms");
        AddMetricCell(grid, 2, 1, "标准差", $"{s.StandardDeviationMs:0.000} ms");
        AddMetricCell(grid, 3, 1, "稳定性", s.StabilityText);

        return grid;
    }

    private static Control CreateGcMetrics(DetailedBenchmarkResult result)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 8
        };

        AddMetricCell(grid, 0, 0, "每次分配", FormatBytes(result.PerCallAllocatedBytes));
        AddMetricCell(grid, 1, 0, "总分配", FormatBytes(result.TotalAllocatedBytes));
        AddMetricCell(grid, 2, 0, "Gen0 GC", $"{result.Gen0Collections} 次");
        AddMetricCell(grid, 3, 0, "Gen1 GC", $"{result.Gen1Collections} 次");
        AddMetricCell(grid, 0, 1, "Gen2 GC", $"{result.Gen2Collections} 次");

        var runs = result.Results.Count;
        var totalMs = result.TimingSummary.RunTimingsMs.Sum();
        var allocRate = totalMs > 0 ? result.TotalAllocatedBytes / (totalMs / 1000.0) : 0;
        AddMetricCell(grid, 1, 1, "分配率", FormatBytes((long)allocRate) + "/s");

        var totalManagedMs = result.TimingSummary.AverageMs * runs;
        AddMetricCell(grid, 2, 1, "总耗时", $"{totalManagedMs:0.0} ms");
        AddMetricCell(grid, 3, 1, "吞吐量", $"{(totalMs > 0 ? runs / (totalMs / 1000.0) : 0):0.0}/s");

        return grid;
    }

    private static Control CreateProfilePanel(ProfileResult? profile)
    {
        if (profile is null || profile.Steps.Length == 0)
        {
            return CreateCard("分步耗时", new TextBlock
            {
                Text = "无分步数据（算法未提供 Profile）",
                FontSize = 12,
                Foreground = TextMuted,
                Margin = new Thickness(0, 4)
            });
        }

        var panel = new StackPanel { Spacing = 4 };
        var maxMs = profile.Steps.Max(s => s.ElapsedMs);

        foreach (var step in profile.Steps)
        {
            var pct = profile.TotalMs > 0 ? step.ElapsedMs / profile.TotalMs * 100 : 0;
            var fraction = maxMs > 0 ? step.ElapsedMs / maxMs : 0;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("220,*,80,50"),
                ColumnSpacing = 8,
                Height = step.Detail is null ? 26 : 34
            };

            // 步骤名称
            var stepLabel = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VerticalAlignment.Center
            };
            stepLabel.Children.Add(new TextBlock
            {
                Text = step.Name,
                FontSize = 12,
                Foreground = TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (step.Detail is not null)
            {
                stepLabel.Children.Add(new TextBlock
                {
                    Text = step.Detail,
                    FontSize = 10,
                    Foreground = TextMuted,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            row.Children.Add(stepLabel);

            // 进度条
            var barTrack = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = BarTrack,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = BarFill,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(2, 200 * fraction)
                }
            };
            Grid.SetColumn(barTrack, 1);
            row.Children.Add(barTrack);

            // 耗时
            var timeText = new TextBlock
            {
                Text = $"{step.ElapsedMs:0.000} ms",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = TextPrimary,
                FontFamily = new FontFamily("Menlo, Consolas, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(timeText, 2);
            row.Children.Add(timeText);

            // 百分比
            var pctText = new TextBlock
            {
                Text = $"{pct:0.0}%",
                FontSize = 11,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(pctText, 3);
            row.Children.Add(pctText);

            panel.Children.Add(row);
        }

        // 总计行
        var totalRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*,80,50"),
            ColumnSpacing = 8,
            Height = 28,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var separator = new Border
        {
            Height = 1,
            Background = CardBorder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumnSpan(separator, 4);
        totalRow.Children.Add(separator);

        totalRow.Children.Add(new TextBlock
        {
            Text = "总计",
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = TextPrimary,
            VerticalAlignment = VerticalAlignment.Bottom
        });
        var totalTime = new TextBlock
        {
            Text = $"{profile.TotalMs:0.000} ms",
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = AccentBlue,
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            VerticalAlignment = VerticalAlignment.Bottom,
            TextAlignment = TextAlignment.Right
        };
        Grid.SetColumn(totalTime, 2);
        totalRow.Children.Add(totalTime);
        var totalPct = new TextBlock
        {
            Text = "100%",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextMuted,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextAlignment = TextAlignment.Right
        };
        Grid.SetColumn(totalPct, 3);
        totalRow.Children.Add(totalPct);
        panel.Children.Add(totalRow);

        return CreateCard($"分步耗时（最后一次运行 · {profile.OperationName}）", panel);
    }

    private static Control CreateChartPanel(BenchmarkSummary summary)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TimingDistributionChart
        {
            Summary = summary,
            MinHeight = 190
        });
        panel.Children.Add(new TextBlock
        {
            Text = "横轴为单次耗时区间，纵轴为落入该区间的运行次数；虚线标记平均、P50 和 P95。",
            FontSize = 11,
            Foreground = TextSecondary
        });

        return CreateCard("耗时分布", panel);
    }

    private static Border CreateCard(string title, Control content)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextPrimary
        });

        var card = new Border
        {
            Background = CardBackground,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            Child = new StackPanel
            {
                Spacing = 0,
                Children = { header, content }
            }
        };
        return card;
    }

    private static void AddMetricCell(Grid grid, int col, int row, string label, string value)
    {
        var tile = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#F3F4F6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 5),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 10,
                        Foreground = TextMuted
                    },
                    new TextBlock
                    {
                        Text = value,
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = TextPrimary,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
        Grid.SetColumn(tile, col);
        Grid.SetRow(tile, row);
        grid.Children.Add(tile);
    }

    private static string Percentile(IReadOnlyList<double> runTimings, double pct)
    {
        if (runTimings.Count == 0) return "-";
        var sorted = runTimings.OrderBy(v => v).ToArray();
        var pos = (sorted.Length - 1) * pct;
        var lower = (int)Math.Floor(pos);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var weight = pos - lower;
        var value = sorted[lower] * (1 - weight) + sorted[upper] * weight;
        return $"{value:0.000} ms";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
            _ => $"{bytes / (1024.0 * 1024.0):0.00} MB"
        };
    }
}

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
