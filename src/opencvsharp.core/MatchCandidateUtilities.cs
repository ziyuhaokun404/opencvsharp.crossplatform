using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace OpenCvSharp.Core;

public static class MatchCandidateUtilities
{
    /// <summary>
    /// 当候选数量超过此阈值且矩形尺寸一致时，自动使用空间网格 NMS。
    /// </summary>
    private const int GridNmsThreshold = 256;
    private const int DenseGridMaxCellsPerCandidate = 4;
    private const int MaxInitialSelectedCapacity = 4096;

    public static List<MatchCandidate> ApplyNonMaximumSuppression(List<MatchCandidate> matches, double overlapThreshold)
    {
        var count = matches.Count;
        if (count == 0) return [];

        // 内部原地排序，消除之前 var sorted = new MatchCandidate[count] 带来的 300+KB GC 内存分配
        matches.Sort(static (a, b) => 
        {
            if (b.Score > a.Score) return 1;
            if (b.Score < a.Score) return -1;
            return 0;
        });

        // 当候选数量较大且所有矩形尺寸一致时，使用空间网格加速
        if (count > GridNmsThreshold && TryGetUniformRectSize(matches, out var rectW, out var rectH))
            return ApplyGridNms(matches, rectW, rectH, overlapThreshold);

        return ApplyBruteForceNms(matches, overlapThreshold);
    }

    /// <summary>
    /// 暴力 NMS：适用于候选数量少或矩形尺寸不一致的场景。O(N×M)。
    /// </summary>
    private static List<MatchCandidate> ApplyBruteForceNms(List<MatchCandidate> sorted, double overlapThreshold)
    {
        var selected = CreateSelectedList(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            var candidate = sorted[i];
            var suppressed = false;
            for (var j = 0; j < selected.Count; j++)
            {
                if (CalculateIntersectionOverUnion(candidate.Rect, selected[j].Rect) > overlapThreshold)
                {
                    suppressed = true;
                    break;
                }
            }
            if (!suppressed)
                selected.Add(candidate);
        }
        return selected;
    }

    /// <summary>
    /// 空间网格 NMS：适用于所有候选矩形尺寸一致的场景（模板匹配的常见情况）。
    /// 将图像空间划分为 (rectW × rectH) 的网格单元，每个候选只需与相邻 3×3 单元中的
    /// 已选中项比较 IoU，将复杂度从 O(N×M) 降为 O(N×k)，其中 k 是每个单元的平均密度。
    /// </summary>
    private static List<MatchCandidate> ApplyGridNms(
        List<MatchCandidate> sorted, int rectW, int rectH, double overlapThreshold)
    {
        var cellW = Math.Max(rectW, 1);
        var cellH = Math.Max(rectH, 1);
        var rectArea = (long)rectW * rectH;
        if (TryGetDenseGridBounds(sorted, cellW, cellH, out var minCellX, out var minCellY, out var gridWidth, out var gridHeight))
            return ApplyDenseGridNms(sorted, rectW, rectH, cellW, cellH, rectArea, overlapThreshold, minCellX, minCellY, gridWidth, gridHeight);

        // 网格：(cellX, cellY) → selected 中该单元链表的头索引
        var grid = new Dictionary<long, int>();
        var nextInCell = new int[sorted.Count];
        var selected = CreateSelectedList(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var candidate = sorted[i];
            var cx = candidate.Rect.X / cellW;
            var cy = candidate.Rect.Y / cellH;

            // 同尺寸矩形的 IoU > 0 条件：|x1-x2| < w 且 |y1-y2| < h，
            // 映射到网格后，只需检查 3×3 邻域
            var suppressed = false;
            for (var dy = -1; dy <= 1 && !suppressed; dy++)
            {
                for (var dx = -1; dx <= 1 && !suppressed; dx++)
                {
                    var key = PackCellKey(cx + dx, cy + dy);
                    if (!grid.TryGetValue(key, out var selectedIndex))
                        continue;

                    while (selectedIndex >= 0)
                    {
                        if (HasUniformOverlapAboveThreshold(candidate.Rect, selected[selectedIndex].Rect, rectW, rectH, rectArea, overlapThreshold))
                        {
                            suppressed = true;
                            break;
                        }
                        selectedIndex = nextInCell[selectedIndex];
                    }
                }
            }

            if (suppressed)
                continue;

            var newSelectedIndex = selected.Count;
            selected.Add(candidate);
            var cellKey = PackCellKey(cx, cy);
            nextInCell[newSelectedIndex] = grid.TryGetValue(cellKey, out var headIndex)
                ? headIndex
                : -1;
            grid[cellKey] = newSelectedIndex;
        }

        return selected;
    }

    private static List<MatchCandidate> ApplyDenseGridNms(
        List<MatchCandidate> sorted,
        int rectW,
        int rectH,
        int cellW,
        int cellH,
        long rectArea,
        double overlapThreshold,
        int minCellX,
        int minCellY,
        int gridWidth,
        int gridHeight)
    {
        var grid = new int[gridWidth * gridHeight];
        Array.Fill(grid, -1);
        var nextInCell = new int[sorted.Count];
        var selected = CreateSelectedList(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var candidate = sorted[i];
            var cx = candidate.Rect.X / cellW - minCellX;
            var cy = candidate.Rect.Y / cellH - minCellY;

            var suppressed = false;
            for (var dy = -1; dy <= 1 && !suppressed; dy++)
            {
                var ny = cy + dy;
                if ((uint)ny >= (uint)gridHeight)
                    continue;

                var rowOffset = ny * gridWidth;
                for (var dx = -1; dx <= 1 && !suppressed; dx++)
                {
                    var nx = cx + dx;
                    if ((uint)nx >= (uint)gridWidth)
                        continue;

                    var selectedIndex = grid[rowOffset + nx];
                    while (selectedIndex >= 0)
                    {
                        if (HasUniformOverlapAboveThreshold(candidate.Rect, selected[selectedIndex].Rect, rectW, rectH, rectArea, overlapThreshold))
                        {
                            suppressed = true;
                            break;
                        }
                        selectedIndex = nextInCell[selectedIndex];
                    }
                }
            }

            if (suppressed)
                continue;

            var newSelectedIndex = selected.Count;
            selected.Add(candidate);
            var gridIndex = cy * gridWidth + cx;
            nextInCell[newSelectedIndex] = grid[gridIndex];
            grid[gridIndex] = newSelectedIndex;
        }

        return selected;
    }

    private static bool TryGetDenseGridBounds(
        List<MatchCandidate> sorted,
        int cellW,
        int cellH,
        out int minCellX,
        out int minCellY,
        out int gridWidth,
        out int gridHeight)
    {
        var first = sorted[0].Rect;
        minCellX = first.X / cellW;
        minCellY = first.Y / cellH;
        var maxCellX = minCellX;
        var maxCellY = minCellY;

        for (var i = 1; i < sorted.Count; i++)
        {
            var rect = sorted[i].Rect;
            var cx = rect.X / cellW;
            var cy = rect.Y / cellH;
            minCellX = Math.Min(minCellX, cx);
            minCellY = Math.Min(minCellY, cy);
            maxCellX = Math.Max(maxCellX, cx);
            maxCellY = Math.Max(maxCellY, cy);
        }

        var width = (long)maxCellX - minCellX + 1;
        var height = (long)maxCellY - minCellY + 1;
        var cellCount = width * height;
        if (width > int.MaxValue || height > int.MaxValue || cellCount > (long)sorted.Count * DenseGridMaxCellsPerCandidate)
        {
            gridWidth = 0;
            gridHeight = 0;
            return false;
        }

        gridWidth = (int)width;
        gridHeight = (int)height;
        return true;
    }

    private static List<MatchCandidate> CreateSelectedList(int candidateCount)
    {
        return new List<MatchCandidate>(Math.Min(candidateCount, MaxInitialSelectedCapacity));
    }

    private static bool HasUniformOverlapAboveThreshold(Rect a, Rect b, int rectW, int rectH, long rectArea, double overlapThreshold)
    {
        var dx = Math.Abs(a.X - b.X);
        if (dx >= rectW) return false;

        var dy = Math.Abs(a.Y - b.Y);
        if (dy >= rectH) return false;

        var intersectionArea = (long)(rectW - dx) * (rectH - dy);
        var unionArea = (rectArea << 1) - intersectionArea;
        return unionArea > 0
            ? intersectionArea > overlapThreshold * unionArea
            : 0 > overlapThreshold;
    }

    /// <summary>
    /// 检查所有候选矩形是否具有相同的宽高（模板匹配的常见情况）。
    /// </summary>
    private static bool TryGetUniformRectSize(List<MatchCandidate> matches, out int rectW, out int rectH)
    {
        rectW = 0;
        rectH = 0;
        if (matches.Count == 0) return false;

        rectW = matches[0].Rect.Width;
        rectH = matches[0].Rect.Height;
        for (var i = 1; i < matches.Count; i++)
        {
            if (matches[i].Rect.Width != rectW || matches[i].Rect.Height != rectH)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 将二维网格坐标打包为 long 键，用于 Dictionary 查找。
    /// </summary>
    private static long PackCellKey(int cx, int cy)
    {
        return ((long)cx << 32) | (uint)cy;
    }

    private static double CalculateIntersectionOverUnion(Rect a, Rect b)
    {
        var intersection = a.Intersect(b);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0;

        var intersectionArea = intersection.Width * intersection.Height;
        var unionArea = a.Width * a.Height + b.Width * b.Height - intersectionArea;
        return unionArea <= 0 ? 0 : (double)intersectionArea / unionArea;
    }
}
