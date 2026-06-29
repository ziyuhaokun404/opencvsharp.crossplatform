using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using OpenCvSharp.CrossPlatform.Core;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaWindow = Avalonia.Controls.Window;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Controls;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Views;

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
