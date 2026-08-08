using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Tools.Surface.Analysis;
using Civil3D.Tools.Surface.Dtos;
using Xunit;
using static Civil3D.Tools.Surface.Tests.TestDoubles;

namespace Civil3D.Tools.Surface.Tests;

/// <summary>
/// The pure comparison engine: per-metric comparisons, differences with severity, optional
/// statistics, recommendations derived only from available metrics, and report serialization.
/// </summary>
public class SurfaceComparerTests
{
    private static SurfaceComparisonData ContrastingData(
        bool includeStatistics = true, bool includeRecommendations = true)
        => new()
        {
            ExistingSurface = SampleData.Existing(),
            ProposedSurface = SampleData.Proposed(),
            IncludeStatistics = includeStatistics,
            IncludeRecommendations = includeRecommendations,
        };

    [Fact]
    public void Compare_ContrastingSurfaces_ProducesReviewRequiredVerdict()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData());

        Assert.Equal("Review Required", result.Summary.Verdict);
        Assert.Equal(6, result.Summary.MetricCount);
        Assert.Equal(5, result.Summary.DifferenceCount);
        Assert.Equal(4, result.Summary.SignificantDifferenceCount);
        Assert.Equal(3, result.Summary.RecommendationCount);
        Assert.Equal("EG", result.Summary.ExistingSurfaceName);
        Assert.Equal("FG", result.Summary.ProposedSurfaceName);
    }

    [Fact]
    public void Compare_ContrastingSurfaces_FlagsElevationAndPointDeltas()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData());

        // Point-count delta 30k of 100k = 30% >= 25% -> significant Warning.
        Assert.Contains(result.Differences, d => d.MetricKey == "pointCount"
            && d.Severity == ComparisonSeverity.Warning);
        // Elevation deltas above the tolerances -> Warning; name difference -> Information.
        Assert.Contains(result.Differences, d => d.MetricKey == "minElevation"
            && d.Severity == ComparisonSeverity.Warning);
        Assert.Contains(result.Differences, d => d.MetricKey == "maxElevation"
            && d.Severity == ComparisonSeverity.Warning);
        Assert.Contains(result.Differences, d => d.MetricKey == "meanElevation"
            && d.Severity == ComparisonSeverity.Warning);
        Assert.Contains(result.Differences, d => d.MetricKey == "name"
            && d.Severity == ComparisonSeverity.Information);
        Assert.DoesNotContain(result.Differences, d => d.MetricKey == "kind");
    }

    [Fact]
    public void Compare_ContrastingSurfaces_ComputesStatistics()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData());

        Assert.NotNull(result.Statistics);
        Assert.Equal(-30_000, result.Statistics!.PointCountDelta);
        Assert.Equal(30.0, result.Statistics.PointCountDeltaPercent, 2);
        Assert.Equal(4.5, result.Statistics.MinElevationDelta, 3);
        Assert.Equal(10.0, result.Statistics.MaxElevationDelta, 3);
        Assert.Equal(7.0, result.Statistics.MeanElevationDelta, 3);
        Assert.Equal(5.5, result.Statistics.ElevationRangeDelta, 3);
    }

    [Fact]
    public void Compare_ContrastingSurfaces_ProducesRecommendations()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData());

        Assert.Contains(result.Recommendations, r => r.Title == "Large point-count difference");
        Assert.Contains(result.Recommendations, r => r.Title == "Large elevation range difference");
        Assert.Contains(result.Recommendations, r => r.Title == "Review before volume calculations");
    }

    [Fact]
    public void Compare_StatisticsDisabled_OmitsStatistics()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData(includeStatistics: false));

        Assert.Null(result.Statistics);
    }

    [Fact]
    public void Compare_RecommendationsDisabled_OmitsRecommendations()
    {
        SurfaceComparisonResult result = SurfaceComparer.Compare(ContrastingData(includeRecommendations: false));

        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void Compare_CompatibleSurfaces_ProducesCompatibleVerdict()
    {
        var data = new SurfaceComparisonData
        {
            ExistingSurface = SampleData.CompatibleExisting(),
            ProposedSurface = SampleData.CompatibleProposed(),
        };

        SurfaceComparisonResult result = SurfaceComparer.Compare(data);

        Assert.Equal("Compatible", result.Summary.Verdict);
        Assert.Equal(0, result.Summary.SignificantDifferenceCount);
        Assert.Contains(result.Recommendations, r => r.Title == "Surfaces are compatible");
    }

    [Fact]
    public void Compare_OutdatedProposedSurface_ProducesOutdatedRecommendation()
    {
        var data = new SurfaceComparisonData
        {
            ExistingSurface = SampleData.OutdatedExisting(),
            ProposedSurface = SampleData.OutdatedProposed(),
        };

        SurfaceComparisonResult result = SurfaceComparer.Compare(data);

        Assert.Contains(result.Recommendations, r => r.Title == "Surface appears outdated");
        Assert.Contains(result.Recommendations, r => r.Title == "Large point-count difference");
    }

    [Fact]
    public void Compare_IdenticalSurfaces_ProducesNoDifferences()
    {
        var data = new SurfaceComparisonData
        {
            ExistingSurface = SampleData.Existing(),
            ProposedSurface = SampleData.Existing() with { Id = 99 },
        };

        SurfaceComparisonResult result = SurfaceComparer.Compare(data);

        Assert.Equal(0, result.Summary.DifferenceCount);
        Assert.Equal(0, result.Summary.SignificantDifferenceCount);
        Assert.Equal("Compatible", result.Summary.Verdict);
        Assert.All(result.Metrics, m => Assert.False(m.IsSignificant));
        Assert.NotNull(result.Statistics);
        Assert.Equal(0, result.Statistics!.PointCountDelta);
        Assert.Equal(0, result.Statistics.MeanElevationDelta, 3);
    }

    [Fact]
    public void Report_Serialization_RoundTrips()
    {
        SurfaceComparisonReport report = new()
        {
            Summary = SurfaceComparer.Compare(ContrastingData()).Summary,
            Metrics = SurfaceComparer.Compare(ContrastingData()).Metrics,
            Differences = SurfaceComparer.Compare(ContrastingData()).Differences,
            Statistics = SurfaceComparer.Compare(ContrastingData()).Statistics,
            Recommendations = SurfaceComparer.Compare(ContrastingData()).Recommendations,
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = "surface.comparison.report",
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Elapsed = TimeSpan.FromMilliseconds(12),
                TotalSteps = 5,
                CompletedSteps = 5,
            },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        SurfaceComparisonReport? round = JsonSerializer.Deserialize<SurfaceComparisonReport>(json, SharedJson.Options);

        Assert.NotNull(round);
        Assert.Equal("Review Required", round!.Summary.Verdict);
        Assert.Equal(6, round.Metrics.Count);
        Assert.Equal(5, round.Differences.Count);
        Assert.NotNull(round.Statistics);
        Assert.Equal(-30_000, round.Statistics!.PointCountDelta);
        Assert.Equal(3, round.Recommendations.Count);
        Assert.Equal("surface.comparison.report", round.Execution.WorkflowName);
        Assert.Contains(round.Recommendations, r => r.Title == "Review before volume calculations");
    }
}
