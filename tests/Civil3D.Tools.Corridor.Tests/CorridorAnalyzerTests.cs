using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Tools.Corridor.Analysis;
using Civil3D.Tools.Corridor.Dtos;
using Xunit;
using static Civil3D.Tools.Corridor.Tests.TestDoubles;

namespace Civil3D.Tools.Corridor.Tests;

/// <summary>
/// The pure analysis engine: health verdicts, issue generation from the available metrics,
/// aggregate statistics, recommendations derived only from exposed corridor data and report
/// serialization.
/// </summary>
public class CorridorAnalyzerTests
{
    [Fact]
    public void Analyze_HealthyCorridor_HealthyVerdict()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze(SampleData.HealthyOnly());

        Assert.Equal("Healthy", result.Verdict);
        CorridorSummary summary = Assert.Single(result.Corridors);
        Assert.Equal("Healthy", summary.Status);
        Assert.Equal(2, summary.BaselineCount);
        Assert.Equal(1, summary.CorridorSurfaceCount);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_NoSurfacesCorridor_ReviewRecommended()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze([SampleData.RampA()]);

        Assert.Equal("Review Recommended", result.Verdict);
        Assert.Equal("No Surfaces", result.Corridors[0].Status);
        Assert.Contains(result.Issues, i => i.Code == "noSurfaces" && i.Severity == CorridorSeverity.Warning);
        Assert.Contains(result.Issues, i => i.Code == "missingDescription");
    }

    [Fact]
    public void Analyze_NoBaselinesCorridor_AttentionRequired()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze([SampleData.Stub()]);

        Assert.Equal("Attention Required", result.Verdict);
        Assert.Equal("No Baselines", result.Corridors[0].Status);
        Assert.Contains(result.Issues, i => i.Code == "noBaselines" && i.Severity == CorridorSeverity.Error);
        Assert.Contains(result.Issues, i => i.Code == "missingStyle" && i.Severity == CorridorSeverity.Warning);
        Assert.Contains(result.Issues, i => i.Code == "missingCodeSetStyle");
    }

    [Fact]
    public void Analyze_EmptySet_NoCorridorsVerdict()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze(SampleData.None());

        Assert.Equal("No Corridors", result.Verdict);
        Assert.Empty(result.Corridors);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Statistics);
        Assert.Equal(0, result.Statistics!.CorridorCount);
    }

    [Fact]
    public void Analyze_AllCorridors_AggregateStatistics()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze(SampleData.All());

        // Stub has an Error-level no-baselines issue, so the set demands attention.
        Assert.Equal("Attention Required", result.Verdict);
        Assert.Equal(3, result.Corridors.Count);
        Assert.Equal(7, result.Issues.Count);

        CorridorStatistics stats = result.Statistics!;
        Assert.Equal(3, stats.CorridorCount);
        Assert.Equal(3, stats.TotalBaselineCount);
        Assert.Equal(1, stats.TotalCorridorSurfaceCount);
        Assert.Equal(2, stats.CorridorsWithBaselines);
        Assert.Equal(1, stats.CorridorsWithoutBaselines);
        Assert.Equal(1, stats.CorridorsWithSurfaces);
        Assert.Equal(2, stats.CorridorsWithoutSurfaces);
        Assert.Equal(1.0, stats.AverageBaselinesPerCorridor);
    }

    [Fact]
    public void Analyze_StatisticsDisabled_ReturnsNull()
    {
        CorridorAnalysisResult result = CorridorAnalyzer.Analyze(SampleData.All(), includeStatistics: false);

        Assert.Null(result.Statistics);
    }

    [Fact]
    public void BuildRecommendations_NoSurfaces_ReviewGeneratedSurfaces()
    {
        IReadOnlyList<CorridorRecommendation> recommendations =
            CorridorAnalyzer.BuildRecommendations([SampleData.RampA()]);

        Assert.Contains(recommendations, r => r.Title == "Review generated surfaces");
        // Ramp A has no corridor surfaces, so it is not suitable for takeoff yet.
        Assert.DoesNotContain(recommendations, r => r.Title == "Suitable for quantity takeoff");
    }

    [Fact]
    public void BuildRecommendations_MissingStyles_ReviewStyleAssignments()
    {
        IReadOnlyList<CorridorRecommendation> recommendations =
            CorridorAnalyzer.BuildRecommendations([SampleData.Stub()]);

        Assert.Contains(recommendations, r => r.Title == "Review style assignments");
    }

    [Fact]
    public void BuildRecommendations_LargeComplexity_FlagsCorridor()
    {
        var big = new CorridorInfo
        {
            Id = 9,
            Name = "Big",
            StyleId = 101,
            CodeSetStyleId = 201,
            AlignmentId = 301,
            BaselineCount = 4,
            CorridorSurfaceCount = 3,
        };

        IReadOnlyList<CorridorRecommendation> recommendations =
            CorridorAnalyzer.BuildRecommendations([big]);

        Assert.Contains(recommendations, r => r.Title == "Large corridor complexity");
        Assert.Contains(recommendations, r => r.Title == "Suitable for quantity takeoff");
    }

    [Fact]
    public void BuildRecommendations_NoCorridors_SingleInformation()
    {
        IReadOnlyList<CorridorRecommendation> recommendations =
            CorridorAnalyzer.BuildRecommendations(SampleData.None());

        CorridorRecommendation recommendation = Assert.Single(recommendations);
        Assert.Equal("No corridors in the drawing", recommendation.Title);
        Assert.Equal(CorridorSeverity.Information, recommendation.Severity);
        Assert.Null(recommendation.RelatedCorridor);
    }

    [Fact]
    public void Report_Serialization_RoundTrips()
    {
        CorridorAnalysisResult analysis = CorridorAnalyzer.Analyze(SampleData.All());
        var report = new CorridorAnalysisReport
        {
            Verdict = analysis.Verdict,
            Corridors = analysis.Corridors,
            Statistics = analysis.Statistics,
            Issues = analysis.Issues,
            Recommendations = CorridorAnalyzer.BuildRecommendations(SampleData.All()),
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = "corridor.analysis.report",
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Elapsed = TimeSpan.FromMilliseconds(5),
                TotalSteps = 5,
                CompletedSteps = 5,
            },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        CorridorAnalysisReport? roundTripped =
            JsonSerializer.Deserialize<CorridorAnalysisReport>(json, SharedJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(report.Verdict, roundTripped!.Verdict);
        Assert.Equal(report.Corridors.Count, roundTripped.Corridors.Count);
        Assert.Equal(report.Issues.Count, roundTripped.Issues.Count);
        Assert.Equal(report.Recommendations.Count, roundTripped.Recommendations.Count);
        Assert.NotNull(roundTripped.Statistics);
        Assert.Equal(3, roundTripped.Statistics!.CorridorCount);
        Assert.Equal("corridor.analysis.report", roundTripped.Execution.WorkflowName);
    }
}
