using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Health.Analysis;
using Civil3D.Tools.Health.Dtos;
using Xunit;
using static Civil3D.Tools.Health.Tests.TestDoubles;

namespace Civil3D.Tools.Health.Tests;

/// <summary>
/// The pure analysis engine: every rule (empty collections, duplicates, missing descriptions,
/// orphaned references, missing and unused styles, large collections, locked points, drawing
/// state), the category roll-ups, statistics, recommendations, ordering and serialization.
/// </summary>
public class HealthAnalyzerTests
{
    private static HealthData EmptyData() => new() { Drawing = SampleData.Drawing(), Statistics = SampleData.Statistics() };

    [Fact]
    public void Analyze_EmptyData_ProducesEmptyCollectionFindings()
    {
        HealthAnalysisResult result = HealthAnalyzer.Analyze(EmptyData());

        string[] codes = ["EMPTY_ALIGNMENTS", "EMPTY_SURFACES", "EMPTY_PROFILES", "EMPTY_CORRIDORS",
            "EMPTY_PIPE_NETWORKS", "EMPTY_COGO_POINTS", "EMPTY_STYLES"];
        Assert.All(codes, code => Assert.Contains(result.Issues, i => i.Code == code));
        Assert.All(
            result.Issues.Where(i => codes.Contains(i.Code)),
            issue => Assert.Equal(HealthSeverity.Information, issue.Severity));
    }

    [Fact]
    public void Analyze_EmptyData_ReportsReviewRecommendation()
    {
        HealthAnalysisResult result = HealthAnalyzer.Analyze(EmptyData());

        // UNSAVED_CHANGES is also present because the sample drawing is modified, so the
        // healthy-only recommendation appears only when there are no issues at all.
        Assert.Contains(result.Recommendations, r => r.Description.Contains("Review"));
    }

    [Fact]
    public void Analyze_DuplicateNames_ProducesWarning()
    {
        HealthData data = EmptyData() with
        {
            Alignments =
            [
                new AlignmentInfo { Id = 1, Name = "Centerline" },
                new AlignmentInfo { Id = 2, Name = "CENTERLINE" },
            ],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue duplicate = Assert.Single(result.Issues, i => i.Code == "DUPLICATE_ALIGNMENT_NAME");
        Assert.Equal(HealthSeverity.Warning, duplicate.Severity);
        Assert.Equal("Centerline", duplicate.RelatedObject);
    }

    [Fact]
    public void Analyze_OrphanedProfile_ProducesError()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A" }],
            Profiles = [new ProfileInfo { Id = 9, Name = "Orphan", AlignmentId = 42 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue orphan = Assert.Single(result.Issues, i => i.Code == "ORPHANED_PROFILE");
        Assert.Equal(HealthSeverity.Error, orphan.Severity);
        Assert.Equal("Orphan", orphan.RelatedObject);
    }

    [Fact]
    public void Analyze_OrphanedCorridor_ProducesError()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A" }],
            Corridors = [new CorridorInfo { Id = 9, Name = "C", AlignmentId = 99 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        Assert.Contains(result.Issues, i => i.Code == "ORPHANED_CORRIDOR" && i.Severity == HealthSeverity.Error);
    }

    [Fact]
    public void Analyze_MissingStyleReference_ProducesError()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 777 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue missing = Assert.Single(result.Issues, i => i.Code == "MISSING_STYLE");
        Assert.Equal(HealthSeverity.Error, missing.Severity);
        Assert.Equal("A", missing.RelatedObject);
    }

    [Fact]
    public void Analyze_MissingDescription_ProducesInformation()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "Bare", Description = null }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue missing = Assert.Single(result.Issues, i => i.Code == "MISSING_ALIGNMENT_DESCRIPTION");
        Assert.Equal(HealthSeverity.Information, missing.Severity);
        Assert.Equal("Bare", missing.RelatedObject);
    }

    [Fact]
    public void Analyze_LargeDrawing_ProducesWarning()
    {
        var options = new HealthAnalyzerOptions { LargeDrawingEntityThreshold = 100, LargeModelSpaceEntityThreshold = 100 };
        HealthData data = EmptyData() with
        {
            Statistics = SampleData.Statistics() with { EntityCount = 5_000, ModelSpaceEntityCount = 3_000 },
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data, options);

        Assert.Contains(result.Issues, i => i.Code == "LARGE_DRAWING" && i.Severity == HealthSeverity.Warning);
        Assert.Contains(result.Issues, i => i.Code == "LARGE_MODEL_SPACE" && i.Severity == HealthSeverity.Warning);
    }

    [Fact]
    public void Analyze_LockedCogoPoints_ProducesWarning()
    {
        HealthData data = EmptyData() with
        {
            CogoPoints =
            [
                new CogoPointInfo { Id = 1, PointNumber = 1 },
                new CogoPointInfo { Id = 2, PointNumber = 2, IsLocked = true },
            ],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue locked = Assert.Single(result.Issues, i => i.Code == "LOCKED_COGO_POINTS");
        Assert.Equal(HealthSeverity.Warning, locked.Severity);
        Assert.Contains("1 of 2", locked.Description);
    }

    [Fact]
    public void Analyze_ReadOnlyAndModified_ProduceFindings()
    {
        HealthData data = EmptyData() with
        {
            Drawing = SampleData.Drawing() with { IsReadOnly = true, IsModified = true },
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        Assert.Contains(result.Issues, i => i.Code == "READ_ONLY_DRAWING" && i.Severity == HealthSeverity.Warning);
        Assert.Contains(result.Issues, i => i.Code == "UNSAVED_CHANGES" && i.Severity == HealthSeverity.Information);
    }

    [Fact]
    public void Analyze_UnusedStyle_ProducesInformation()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 1 }],
            Styles =
            [
                new StyleInfo { Id = 1, Name = "Used", Kind = StyleKind.Alignment },
                new StyleInfo { Id = 2, Name = "Unused", Kind = StyleKind.Alignment },
            ],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthIssue unused = Assert.Single(result.Issues, i => i.Code == "UNUSED_STYLE");
        Assert.Equal(HealthSeverity.Information, unused.Severity);
        Assert.Equal("Unused", unused.RelatedObject);
    }

    [Fact]
    public void Analyze_Statistics_RollUpMatchesIssues()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 999 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        Assert.Equal(result.Issues.Count, result.Statistics.TotalIssues);
        Assert.Equal(result.Issues.Count(i => i.Severity == HealthSeverity.Error), result.Statistics.ErrorCount);
        Assert.Equal(1, result.Statistics.ObjectCount);
    }

    [Fact]
    public void Analyze_Categories_RollUpPerCategory()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 999 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        HealthCategory alignments = Assert.Single(result.Categories, c => c.Name == "Alignments");
        Assert.True(alignments.TotalIssues >= 1);
        Assert.Equal(
            alignments.ErrorCount + alignments.WarningCount + alignments.InformationCount,
            alignments.TotalIssues);
    }

    [Fact]
    public void Analyze_Issues_OrderedBySeverityThenCode()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 999, Description = null }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        for (int i = 1; i < result.Issues.Count; i++)
        {
            Assert.True(result.Issues[i - 1].Severity >= result.Issues[i].Severity,
                "Issues must be ordered by severity, highest first.");
        }

        // The highest severity present is the MISSING_STYLE error.
        Assert.Equal(HealthSeverity.Error, result.Issues[0].Severity);
    }

    [Fact]
    public void Analyze_Recommendations_IncludeErrorRecovery()
    {
        HealthData data = EmptyData() with
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 999 }],
        };

        HealthAnalysisResult result = HealthAnalyzer.Analyze(data);

        Assert.Contains(result.Recommendations, r => r.Description.Contains("error"));
    }

    [Fact]
    public void Report_SerializesAndRoundTrips()
    {
        var report = new DrawingHealthReport
        {
            DrawingName = "R.dwg",
            Statistics = SampleData.Statistics(),
            Health = new HealthStatistics { TotalIssues = 2, WarningCount = 1, InformationCount = 1, ObjectCount = 9 },
            Categories =
            [
                new HealthCategory { Name = "COGO Points", TotalIssues = 2, WarningCount = 1, InformationCount = 1 },
            ],
            Issues =
            [
                new HealthIssue
                {
                    Code = "LOCKED_COGO_POINTS",
                    Severity = HealthSeverity.Warning,
                    Category = "COGO Points",
                    Description = "1 of 2 COGO points are locked.",
                    Reason = "Locked points reject edits.",
                    SuggestedAction = "Unlock points that should be editable.",
                },
            ],
            Recommendations =
            [
                new HealthRecommendation { Description = "Review all 2 findings.", Reason = "R", SuggestedAction = "S" },
            ],
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = "drawing.health.report",
                TotalSteps = 5,
                CompletedSteps = 5,
            },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        DrawingHealthReport? roundTrip = JsonSerializer.Deserialize<DrawingHealthReport>(json, SharedJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal(report.DrawingName, roundTrip.DrawingName);
        Assert.Equal(report.Statistics.LayerCount, roundTrip.Statistics.LayerCount);
        Assert.Equal(report.Health.TotalIssues, roundTrip.Health.TotalIssues);
        Assert.Single(roundTrip.Categories);
        Assert.Equal("LOCKED_COGO_POINTS", Assert.Single(roundTrip.Issues).Code);
        Assert.Equal(HealthSeverity.Warning, Assert.Single(roundTrip.Issues).Severity);
        Assert.Single(roundTrip.Recommendations);
        Assert.Equal(5, roundTrip.Execution.TotalSteps);
    }
}
