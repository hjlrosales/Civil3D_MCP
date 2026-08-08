using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Tools.Project.Analysis;
using Civil3D.Tools.Project.Dtos;
using Xunit;
using static Civil3D.Tools.Project.Tests.TestDoubles;

namespace Civil3D.Tools.Project.Tests;

/// <summary>
/// The pure analysis engine: overview, inventory (with name caps), reference integrity,
/// complexity classification across all four bands, statistics, recommendations and report
/// serialization.
/// </summary>
public class ProjectAnalyzerTests
{
    private static ProjectData SampleData() => new()
    {
        Drawing = TestDoubles.SampleData.Drawing(),
        Statistics = TestDoubles.SampleData.Statistics(),
        Alignments = TestDoubles.SampleData.Alignments(),
        Surfaces = TestDoubles.SampleData.Surfaces(),
        Profiles = TestDoubles.SampleData.Profiles(),
        Corridors = TestDoubles.SampleData.Corridors(),
        PipeNetworks = TestDoubles.SampleData.PipeNetworks(),
        CogoPoints = TestDoubles.SampleData.CogoPoints(),
        Styles = TestDoubles.SampleData.Styles(),
    };

    [Fact]
    public void Analyze_SampleData_ProducesCompleteReport()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        Assert.Equal("ProjectSample.dwg", result.Overview.DrawingName);
        Assert.Equal(2, result.Inventory.AlignmentCount);
        Assert.Equal(1, result.Inventory.PipeCount);
        Assert.Equal(1, result.Inventory.StructureCount);
        Assert.Equal(12, result.Inventory.LayerCount);
        Assert.Equal(2, result.Inventory.XRefCount);
        Assert.Contains("Main Road", result.Inventory.AlignmentNames);
        Assert.Equal(2, result.Inventory.StyleCount);
    }

    [Fact]
    public void Analyze_SampleData_ReferencesAreHealthy()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        Assert.True(result.References.IsHealthy);
        Assert.Equal(0, result.References.MissingReferenceCount);
        Assert.Equal(7, result.References.TotalReferencesChecked);
        Assert.Equal(7, result.References.HealthyReferenceCount);
        Assert.Equal("Healthy", result.References.Status);
    }

    [Fact]
    public void Analyze_OrphanedProfile_ReportsMissingReference()
    {
        ProjectData data = SampleData() with
        {
            Profiles = [new ProfileInfo { Id = 9, Name = "Orphan", AlignmentId = 42 }],
        };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.False(result.References.IsHealthy);
        Assert.Equal(1, result.References.MissingReferenceCount);
        Assert.Equal(1, result.References.OrphanedObjectCount);
        Assert.Equal("Issues Found", result.References.Status);
    }

    [Fact]
    public void Analyze_MissingStyle_ReportsMissingStyleReference()
    {
        ProjectData data = SampleData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 999 }],
        };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.False(result.References.IsHealthy);
        Assert.Equal(1, result.References.MissingStyleCount);
        Assert.Contains(result.Recommendations, r => r.Title == "Audit broken references");
    }

    [Fact]
    public void Analyze_SampleData_ComplexityIsMedium()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        // 3400/5000 + 2*3 + 9/20 + 4 + 3 + 2 ≈ 16.1 → Medium (10 ≤ score < 25).
        Assert.Equal(ProjectComplexity.Medium, result.Complexity.Classification);
        Assert.InRange(result.Complexity.Score, 15, 18);
    }

    [Fact]
    public void Analyze_MinimalData_ClassifiesSmall()
    {
        var data = new ProjectData
        {
            Drawing = TestDoubles.SampleData.Drawing(),
            Statistics = TestDoubles.SampleData.Statistics() with { EntityCount = 100, XRefCount = 0 },
        };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.Equal(ProjectComplexity.Small, result.Complexity.Classification);
    }

    [Fact]
    public void Analyze_LargeData_ClassifiesEnterprise()
    {
        ProjectData data = SampleData() with
        {
            Statistics = TestDoubles.SampleData.Statistics() with { EntityCount = 500_000, XRefCount = 40 },
        };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.Equal(ProjectComplexity.Enterprise, result.Complexity.Classification);
        Assert.True(result.Complexity.Score >= 50);
    }

    [Fact]
    public void Analyze_HeavyData_ClassifiesLarge()
    {
        ProjectData data = SampleData() with
        {
            Statistics = TestDoubles.SampleData.Statistics() with { EntityCount = 100_000, XRefCount = 4 },
        };

        // 100000/5000 + 4*3 + 0.5 + 4 + 3 + 2 = 41.5 → Large (25 <= score < 50).
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.Equal(ProjectComplexity.Large, result.Complexity.Classification);
        Assert.InRange(result.Complexity.Score, 40, 43);
    }

    [Fact]
    public void Analyze_Thresholds_AreConfigurable()
    {
        var options = new ProjectSummaryOptions { SmallScoreThreshold = 1_000, MediumScoreThreshold = 2_000, LargeScoreThreshold = 3_000 };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData(), options);

        Assert.Equal(ProjectComplexity.Small, result.Complexity.Classification);
    }

    [Fact]
    public void Analyze_SampleData_GeneratesRecommendations()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        // One unused style, one alignment without a description, and 2 xrefs.
        Assert.Contains(result.Recommendations, r => r.Title == "Review unused styles");
        Assert.Contains(result.Recommendations, r => r.Title == "Missing metadata");
        Assert.Contains(result.Recommendations, r => r.Title == "Reference synchronization");
    }

    [Fact]
    public void Analyze_Recommendations_OrderedByPriority()
    {
        ProjectData data = SampleData() with
        {
            Profiles = [new ProfileInfo { Id = 9, Name = "Orphan", AlignmentId = 42 }],
        };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(data);

        Assert.Equal("Audit broken references", result.Recommendations[0].Title);
        Assert.Equal(RecommendationPriority.High, result.Recommendations[0].Priority);
        for (int i = 1; i < result.Recommendations.Count; i++)
        {
            Assert.True(result.Recommendations[i - 1].Priority >= result.Recommendations[i].Priority);
        }
    }

    [Fact]
    public void Analyze_HealthyData_NoBrokenReferenceRecommendation()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        Assert.DoesNotContain(result.Recommendations, r => r.Title == "Audit broken references");
    }

    [Fact]
    public void Analyze_Inventory_NameListsCapped()
    {
        var options = new ProjectSummaryOptions { MaxNameListLength = 1 };

        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData(), options);

        Assert.Single(result.Inventory.AlignmentNames);
        Assert.True(result.Inventory.NamesTruncated);
        Assert.Equal(2, result.Inventory.AlignmentCount); // The count still reflects all objects.
    }

    [Fact]
    public void Analyze_Statistics_TotalsMatch()
    {
        ProjectAnalysisResult result = ProjectAnalyzer.Analyze(SampleData());

        Assert.Equal(10, result.Statistics.TotalDomainObjects);
        Assert.Equal(3_400, result.Statistics.TotalEntities);
        Assert.Equal(2, result.Statistics.TotalXRefs);
        Assert.Equal(7, result.Statistics.TotalReferencesChecked);
        Assert.Equal(0, result.Statistics.MissingReferenceCount);
    }

    [Fact]
    public void Report_SerializesAndRoundTrips()
    {
        var report = new ProjectSummaryReport
        {
            Overview = new ProjectOverview { DrawingName = "P.dwg", Civil3DVersion = "25.0" },
            Inventory = new ObjectInventory { AlignmentCount = 2, EntityCount = 3_400, XRefCount = 2 },
            References = new ReferenceSummary { IsHealthy = true, TotalReferencesChecked = 7, Status = "Healthy" },
            Complexity = new ComplexityAssessment { Classification = ProjectComplexity.Medium, Score = 16.1, Reason = "R" },
            Statistics = new ProjectStatistics { TotalDomainObjects = 9 },
            Recommendations =
            [
                new ProjectRecommendation
                {
                    Title = "Missing metadata",
                    Description = "1 object(s) have no description.",
                    Priority = RecommendationPriority.Low,
                    SuggestedAction = "Add short descriptions.",
                },
            ],
            Execution = new WorkflowExecutionSummary { WorkflowName = "project.summary.report", TotalSteps = 5, CompletedSteps = 5 },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        ProjectSummaryReport? roundTrip = JsonSerializer.Deserialize<ProjectSummaryReport>(json, SharedJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal(report.Overview.DrawingName, roundTrip.Overview.DrawingName);
        Assert.Equal(report.Inventory.AlignmentCount, roundTrip.Inventory.AlignmentCount);
        Assert.True(roundTrip.References.IsHealthy);
        Assert.Equal(ProjectComplexity.Medium, roundTrip.Complexity.Classification);
        Assert.Equal(RecommendationPriority.Low, Assert.Single(roundTrip.Recommendations).Priority);
        Assert.Equal(5, roundTrip.Execution.TotalSteps);
    }
}
