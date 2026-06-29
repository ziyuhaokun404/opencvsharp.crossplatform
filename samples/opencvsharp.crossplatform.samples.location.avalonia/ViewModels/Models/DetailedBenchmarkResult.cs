using System.Collections.Generic;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

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
