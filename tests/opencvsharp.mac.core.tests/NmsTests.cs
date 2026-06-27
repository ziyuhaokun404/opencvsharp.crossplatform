using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.Mac.Core;
using Xunit;

namespace OpenCvSharp.Mac.Core.Tests;

public class NmsTests
{
    private static MatchCandidate Create(float score, int x, int y, int w = 10, int h = 10)
        => new(new Rect(x, y, w, h), score);

    [Fact]
    public void ApplyNms_NoOverlap_AllCandidatesSurvive()
    {
        var candidates = new List<MatchCandidate>
        {
            Create(0.9f, 0, 0), Create(0.8f, 50, 0), Create(0.7f, 0, 50),
            Create(0.6f, 50, 50), Create(0.5f, 100, 100),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void ApplyNms_IdenticalRects_OnlyHighestScoreSurvives()
    {
        var candidates = new List<MatchCandidate>
        {
            Create(0.3f, 5, 5), Create(0.9f, 5, 5), Create(0.5f, 5, 5),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        Assert.Single(result);
        Assert.Equal(0.9f, result[0].Score);
    }

    [Fact]
    public void ApplyNms_ThresholdZero_SuppressesAnyOverlap()
    {
        // threshold=0: 任何 IoU>0 即被抑制
        var candidates = new List<MatchCandidate>
        {
            Create(0.9f, 0, 0), Create(0.8f, 5, 5),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.0);

        // 仅最高分的幸存
        Assert.Single(result);
        Assert.Equal(0.9f, result[0].Score);
    }

    [Fact]
    public void ApplyNms_HighThreshold_PreservesOverlappingCandidates()
    {
        // threshold=1.0: 仅 IoU=1.0（完全相同）才抑制
        var candidates = new List<MatchCandidate>
        {
            Create(0.9f, 0, 0), Create(0.8f, 5, 5),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 1.0);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyNms_PartialOverlap_MidThreshold()
    {
        // 两个 10x10 矩形偏移 5px → IoU = 25/175 ≈ 0.143
        // threshold=0.3 下不被抑制
        var candidates = new List<MatchCandidate>
        {
            Create(0.9f, 0, 0), Create(0.8f, 5, 5),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.3);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyNms_PartialOverlap_HighSuppressorScore_SurvivorKept()
    {
        // 两个 10x10 矩形偏移 (1,1)：IoU = 81/119 ≈ 0.681 > 0.5
        // 0.8 分的先被选中（排序后它在前面？否——按分数降序，0.9 在前）
        // 因此 0.9 先入选，0.8 因 IoU>0.5 被抑制
        var candidates = new List<MatchCandidate>
        {
            Create(0.8f, 0, 0), Create(0.9f, 1, 1),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        // 按分数降序排序后 0.9 排首位并被选中；0.8 因重叠被抑制
        Assert.Single(result);
        Assert.Equal(0.9f, result[0].Score);
    }

    [Fact]
    public void ApplyNms_ScoresPreservedOrder_ByDescendingScore()
    {
        var candidates = new List<MatchCandidate>
        {
            Create(0.5f, 0, 0), Create(0.9f, 100, 100),
        };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        Assert.Equal(2, result.Count);
        Assert.Equal(0.9f, result[0].Score);
        Assert.Equal(0.5f, result[1].Score);
    }

    [Fact]
    public void ApplyNms_EmptyInput_ReturnsEmptyList()
    {
        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(new List<MatchCandidate>(), 0.5);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyNms_SingleCandidate_ReturnsSame()
    {
        var candidates = new List<MatchCandidate> { Create(0.85f, 10, 20) };

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        Assert.Single(result);
        Assert.Equal(0.85f, result[0].Score);
    }

    [Fact]
    public void ApplyNms_LargeNonOverlappingSet_AllSurvive()
    {
        // 100 个候选，每个间距 50px（不重叠）
        var candidates = new List<MatchCandidate>();
        for (var i = 0; i < 100; i++)
        {
            candidates.Add(Create(0.5f + i * 0.001f, i * 50, 0));
        }

        var result = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, 0.5);

        Assert.Equal(100, result.Count);
    }
}
