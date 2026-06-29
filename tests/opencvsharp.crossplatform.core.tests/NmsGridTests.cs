using System.Collections.Generic;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core.Matching;
using OpenCvSharp.CrossPlatform.Core.Selection;
using Xunit;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

public class NmsGridTests
{
    private static MatchCandidate Create(float score, int x, int y, int size = 10)
        => new(new Rect(x, y, size, size), score);

    private static List<MatchCandidate> ReferenceBruteForceNms(List<MatchCandidate> matches, double overlapThreshold)
    {
        var sorted = new List<MatchCandidate>(matches);
        sorted.Sort(static (a, b) =>
        {
            if (b.Score > a.Score) return 1;
            if (b.Score < a.Score) return -1;
            return 0;
        });

        var selected = new List<MatchCandidate>(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            var candidate = sorted[i];
            var suppressed = false;
            for (var j = 0; j < selected.Count; j++)
            {
                if (MatchCandidateUtilities.CalculateIntersectionOverUnion(candidate.Rect, selected[j].Rect) > overlapThreshold)
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

    private static void AssertEquivalentResults(
        IReadOnlyList<MatchCandidate> expected,
        IReadOnlyList<MatchCandidate> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Score, actual[i].Score, precision: 6);
            Assert.Equal(expected[i].Rect, actual[i].Rect);
        }
    }

    [Fact]
    public void ApplyNms_MoreThan256UniformRects_MatchesReferenceImplementation()
    {
        const int count = 320;
        const int spacing = 12;
        var candidates = new List<MatchCandidate>(count);
        for (var i = 0; i < count; i++)
            candidates.Add(Create(0.99f - i * 0.0001f, i * spacing, 0));

        const double threshold = 0.5;
        var expected = ReferenceBruteForceNms(candidates, threshold);
        var actual = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, threshold);

        AssertEquivalentResults(expected, actual);
    }

    [Fact]
    public void ApplyNms_MoreThan256OverlappingUniformRects_MatchesReferenceImplementation()
    {
        const int count = 300;
        var candidates = new List<MatchCandidate>(count);
        for (var i = 0; i < count; i++)
            candidates.Add(Create(1.0f - i * 0.001f, i % 20, i / 20));

        const double threshold = 0.3;
        var expected = ReferenceBruteForceNms(candidates, threshold);
        var actual = MatchCandidateUtilities.ApplyNonMaximumSuppression(candidates, threshold);

        AssertEquivalentResults(expected, actual);
    }
}
