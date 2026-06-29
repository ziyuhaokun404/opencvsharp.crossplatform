using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class MatchCandidateUtilitiesIouTests
{
    [Fact]
    public void CalculateIoU_ReturnsZero_ForNonOverlappingRects()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(100, 100, 10, 10);

        var iou = MatchCandidateUtilities.CalculateIntersectionOverUnion(a, b);

        Assert.Equal(0, iou);
    }

    [Fact]
    public void CalculateIoU_ReturnsOne_ForIdenticalRects()
    {
        var a = new Rect(5, 5, 20, 30);
        var b = new Rect(5, 5, 20, 30);

        var iou = MatchCandidateUtilities.CalculateIntersectionOverUnion(a, b);

        Assert.Equal(1.0, iou, 10);
    }

    [Fact]
    public void CalculateIoU_ComputesCorrectValue_ForPartialOverlap()
    {
        // 10x10 a, 10x10 b, 5x5 交叠
        // intersection = 25, union = 100 + 100 - 25 = 175 → 25/175 = 1/7
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        var iou = MatchCandidateUtilities.CalculateIntersectionOverUnion(a, b);

        Assert.Equal(1.0 / 7.0, iou, 10);
    }

    [Fact]
    public void CalculateIoU_ReturnsZero_ForTouchingEdges()
    {
        // 仅边缘相切，无面积重叠
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(10, 0, 10, 10);

        var iou = MatchCandidateUtilities.CalculateIntersectionOverUnion(a, b);

        Assert.Equal(0, iou);
    }
}
