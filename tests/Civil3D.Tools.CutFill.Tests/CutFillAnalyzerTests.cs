using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Analysis;
using Civil3D.Tools.CutFill.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.CutFill.Tests.TestDoubles;

namespace Civil3D.Tools.CutFill.Tests;

/// <summary>
/// The pure analysis engine plus the calculator abstraction: verdicts, recommendations derived
/// only from calculated values, optional statistics, the structured not-supported result of the
/// production calculator and report serialization.
/// </summary>
public class CutFillAnalyzerTests
{
    private static CutFillCalculationData ContrastingData() => new()
    {
        ExistingSurface = SampleData.Existing(),
        ProposedSurface = SampleData.Proposed(),
    };

    [Fact]
    public void Analyze_CutDominant_ProducesPredominantlyCutVerdict()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant());

        Assert.Equal("Predominantly Cut", result.Summary.Verdict);
        Assert.False(result.Summary.IsBalanced);
        Assert.Equal(12_000, result.Summary.CutVolume);
        Assert.Equal(4_000, result.Summary.FillVolume);
        Assert.Equal(8_000, result.Summary.NetVolume);
        Assert.Equal(25_000, result.Summary.SurfaceAreaUsed);
        Assert.Equal(CutFillStatus.Computed, result.Summary.Status);
        Assert.Equal("EG", result.Summary.ExistingSurfaceName);
        Assert.Equal("FG", result.Summary.ProposedSurfaceName);
    }

    [Fact]
    public void Analyze_CutDominant_ProducesRecommendations()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant());

        Assert.Contains(result.Recommendations, r => r.Title == "Predominantly cut");
        Assert.Contains(result.Recommendations, r => r.Title == "Significant net export");
        Assert.Contains(result.Recommendations, r => r.Title == "Verify surface quality before construction");
        Assert.DoesNotContain(result.Recommendations, r => r.Title == "Balanced earthwork");
        Assert.DoesNotContain(result.Recommendations, r => r.Title == "Significant net import");
    }

    [Fact]
    public void Analyze_CutDominant_ComputesStatistics()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant());

        Assert.NotNull(result.Statistics);
        Assert.Equal(75.0, result.Statistics!.CutPercentOfTotal, 2);
        Assert.Equal(25.0, result.Statistics.FillPercentOfTotal, 2);
        Assert.Equal(50.0, result.Statistics.NetPercentOfTotal, 2);
        Assert.Equal(3.0, result.Statistics.CutFillRatio, 3);
    }

    [Fact]
    public void Analyze_Balanced_ProducesBalancedVerdict()
    {
        var data = new CutFillCalculationData
        {
            ExistingSurface = SampleData.BalancedExisting(),
            ProposedSurface = SampleData.BalancedProposed(),
        };

        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(data, SampleData.Balanced());

        Assert.Equal("Balanced Earthwork", result.Summary.Verdict);
        Assert.True(result.Summary.IsBalanced);
        Assert.Contains(result.Recommendations, r => r.Title == "Balanced earthwork");
    }

    [Fact]
    public void Analyze_ZeroVolumes_ProducesNoEarthworkVerdict()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.ZeroVolumes());

        Assert.Equal("No Earthwork", result.Summary.Verdict);
        Assert.Null(result.Statistics);
        Assert.Contains(result.Recommendations, r => r.Title == "No earthwork required");
    }

    [Fact]
    public void Analyze_StatisticsDisabled_OmitsStatistics()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(
            ContrastingData(), SampleData.CutDominant(), includeStatistics: false);

        Assert.Null(result.Statistics);
    }

    [Fact]
    public void Analyze_RecommendationsDisabled_OmitsRecommendations()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(
            ContrastingData(), SampleData.CutDominant(), includeRecommendations: false);

        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void Analyze_NotSupported_CarriesStructuredReason()
    {
        var notSupported = new CutFillCalculationResult
        {
            Status = CutFillStatus.NotSupported,
            NotSupportedReason = "Read-only volumes are unavailable.",
        };

        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), notSupported);

        Assert.Equal("Not Supported", result.Summary.Verdict);
        Assert.Equal(CutFillStatus.NotSupported, result.Summary.Status);
        Assert.Equal("Read-only volumes are unavailable.", result.Summary.NotSupportedReason);
        Assert.Null(result.Statistics);
        Assert.Empty(result.Recommendations);
        Assert.Equal(4, result.Differences.Count);
    }

    [Fact]
    public void Analyze_Differences_ContextualiseVolumes()
    {
        CutFillAnalysisResult result = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant());

        Assert.Equal(4, result.Differences.Count);
        Assert.Contains(result.Differences, d => d.MetricKey == "pointCount");
        Assert.Contains(result.Differences, d => d.MetricKey == "meanElevation");
    }

    [Fact]
    public void Analyze_CustomBalanceThreshold_VerdictMatchesIsBalanced()
    {
        // net 3_000 / total 20_000 = 0.15: above the default 10% threshold but below a
        // configured 30% threshold, so the verdict must follow the configured value.
        var data = new CutFillCalculationData
        {
            ExistingSurface = SampleData.Existing(),
            ProposedSurface = SampleData.Proposed(),
            Options = new CutFillOptions { BalanceThreshold = 0.30 },
        };
        var result = new CutFillCalculationResult
        {
            Status = CutFillStatus.Computed,
            CutVolume = 11_500,
            FillVolume = 8_500,
            NetVolume = 3_000,
            SurfaceAreaUsed = 25_000,
        };

        CutFillAnalysisResult analysis = CutFillAnalyzer.Analyze(data, result);

        Assert.Equal("Balanced Earthwork", analysis.Summary.Verdict);
        Assert.True(analysis.Summary.IsBalanced);
        Assert.Contains(analysis.Recommendations, r => r.Title == "Balanced earthwork");
        Assert.DoesNotContain(analysis.Recommendations, r => r.Title == "Predominantly cut");

        // With the default 10% threshold the same result is not balanced.
        CutFillAnalysisResult defaultAnalysis = CutFillAnalyzer.Analyze(
            new CutFillCalculationData
            {
                ExistingSurface = SampleData.Existing(),
                ProposedSurface = SampleData.Proposed(),
            },
            result);

        Assert.Equal("Predominantly Cut", defaultAnalysis.Summary.Verdict);
        Assert.False(defaultAnalysis.Summary.IsBalanced);
    }

    [Fact]
    public void ProductionCalculator_ReturnsStructuredNotSupported()
    {
        var calculator = new Civil3DCutFillCalculator(NullLogger<Civil3DCutFillCalculator>.Instance);

        CutFillCalculationResult result = calculator.Calculate(ContrastingData());

        Assert.Equal(CutFillStatus.NotSupported, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.NotSupportedReason));
        Assert.Equal(0, result.CutVolume);
        Assert.Equal(0, result.FillVolume);
        Assert.Equal(0, result.NetVolume);
    }

    [Fact]
    public void Report_Serialization_RoundTrips()
    {
        CutFillReport report = new()
        {
            Summary = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant()).Summary,
            Differences = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant()).Differences,
            Statistics = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant()).Statistics,
            Recommendations = CutFillAnalyzer.Analyze(ContrastingData(), SampleData.CutDominant()).Recommendations,
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = "calculate.cut.fill",
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Elapsed = TimeSpan.FromMilliseconds(12),
                TotalSteps = 6,
                CompletedSteps = 6,
            },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        CutFillReport? round = JsonSerializer.Deserialize<CutFillReport>(json, SharedJson.Options);

        Assert.NotNull(round);
        Assert.Equal("Predominantly Cut", round!.Summary.Verdict);
        Assert.Equal(12_000, round.Summary.CutVolume);
        Assert.Equal(4, round.Differences.Count);
        Assert.NotNull(round.Statistics);
        Assert.Equal(75.0, round.Statistics!.CutPercentOfTotal, 2);
        Assert.Equal(3, round.Recommendations.Count);
        Assert.Equal("calculate.cut.fill", round.Execution.WorkflowName);
        Assert.Contains(round.Recommendations, r => r.Title == "Significant net export");
    }
}
