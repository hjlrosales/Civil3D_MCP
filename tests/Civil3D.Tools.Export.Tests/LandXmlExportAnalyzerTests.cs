using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Analysis;
using Civil3D.Tools.Export.Dtos;
using Xunit;
using static Civil3D.Tools.Export.Tests.TestDoubles;

namespace Civil3D.Tools.Export.Tests;

/// <summary>
/// The pure analysis engine, the output validator and report serialization: export summaries
/// and statistics, recommendations derived only from the actual outcome, basic XML
/// well-formedness checks with real temp files, and the report round-trip.
/// </summary>
public class LandXmlExportAnalyzerTests
{
    [Fact]
    public void Analyze_Exported_PopulatesSummaryAndStatistics()
    {
        LandXmlExportData data = Data(alignments: 2, profiles: 3, surfaces: 1);
        LandXmlExportResult result = ExportedResult(exported: 3, skipped: 2, fileSize: 1024);

        LandXmlAnalysisResult analysis = LandXmlExportAnalyzer.Analyze(data, result);

        Assert.Equal("Exported", analysis.Summary.Status);
        Assert.Equal(@"C:\out\site.xml", analysis.Summary.OutputPath);
        Assert.Equal(1024, analysis.Summary.FileSizeBytes);
        Assert.Equal(3, analysis.Summary.ExportedCount);
        Assert.Equal(2, analysis.Summary.SkippedCount);
        Assert.Null(analysis.Summary.NotSupportedReason);

        Assert.Equal(2, analysis.Statistics.AlignmentCount);
        Assert.Equal(3, analysis.Statistics.ProfileCount);
        Assert.Equal(1, analysis.Statistics.SurfaceCount);
        Assert.Equal(0, analysis.Statistics.CorridorCount);
        Assert.Equal(0, analysis.Statistics.PipeNetworkCount);
        Assert.Equal(6, analysis.Statistics.TotalCollected);
        Assert.Equal(3, analysis.Statistics.ExportedCount);
        Assert.Equal(2, analysis.Statistics.SkippedCount);

        Assert.Contains(analysis.Recommendations, r => r.Title == "Review skipped objects");
    }

    [Fact]
    public void Analyze_DisabledTypes_AreNotCounted()
    {
        var corridorsEnabled = new LandXmlExportData
        {
            OutputPath = "site.xml",
            IncludeAlignments = true,
            IncludeCorridors = true,
            IncludePipeNetworks = true,
            AlignmentCount = 1,
            CorridorCount = 5,
            PipeNetworkCount = 2,
        };

        LandXmlAnalysisResult analysis =
            LandXmlExportAnalyzer.Analyze(corridorsEnabled, ExportedResult(exported: 0, skipped: 0, fileSize: 50));

        Assert.Equal(5, analysis.Statistics.CorridorCount);
        Assert.Equal(2, analysis.Statistics.PipeNetworkCount);
        Assert.Equal(8, analysis.Statistics.TotalCollected);

        var corridorsDisabled = corridorsEnabled with { IncludeCorridors = false, IncludePipeNetworks = false };
        LandXmlAnalysisResult disabled =
            LandXmlExportAnalyzer.Analyze(corridorsDisabled, ExportedResult(exported: 0, skipped: 0, fileSize: 50));

        Assert.Equal(0, disabled.Statistics.CorridorCount);
        Assert.Equal(0, disabled.Statistics.PipeNetworkCount);
        Assert.Equal(1, disabled.Statistics.TotalCollected);
    }

    [Fact]
    public void Analyze_NotSupported_PopulatesReasonAndRecommendation()
    {
        LandXmlExportData data = Data(alignments: 2, profiles: 3, surfaces: 1);
        LandXmlExportResult result = new()
        {
            Status = LandXmlExportStatus.NotSupported,
            Reason = "Requires a live interactive document context.",
            OutputPath = data.OutputPath,
            FileSizeBytes = 0,
            ExportedObjects = [],
            SkippedObjects = [],
            CompletedAtUtc = null,
        };

        LandXmlAnalysisResult analysis = LandXmlExportAnalyzer.Analyze(data, result);

        Assert.Equal("Not Supported", analysis.Summary.Status);
        Assert.Equal("Requires a live interactive document context.", analysis.Summary.NotSupportedReason);
        Assert.Equal(0, analysis.Summary.FileSizeBytes);
        Assert.Equal(0, analysis.Summary.ExportedCount);
        Assert.Contains(analysis.Recommendations, r => r.Title == "Export not supported by installed API");
    }

    [Fact]
    public void Analyze_NoObjectsToExport_InformationRecommendation()
    {
        LandXmlExportData data = Data(alignments: 0, profiles: 0, surfaces: 0);
        LandXmlAnalysisResult analysis =
            LandXmlExportAnalyzer.Analyze(data, ExportedResult(exported: 0, skipped: 0, fileSize: 10));

        Assert.Equal(0, analysis.Statistics.TotalCollected);
        Assert.Contains(analysis.Recommendations, r => r.Title == "No objects to export");
    }

    [Fact]
    public void Analyze_CleanExport_SuccessRecommendation()
    {
        LandXmlExportData data = Data(alignments: 2, profiles: 3, surfaces: 1);
        LandXmlAnalysisResult analysis =
            LandXmlExportAnalyzer.Analyze(data, ExportedResult(exported: 6, skipped: 0, fileSize: 2048));

        Assert.Contains(analysis.Recommendations, r => r.Title == "Export completed successfully");
        Assert.DoesNotContain(analysis.Recommendations, r => r.Title == "Review skipped objects");
    }

    [Fact]
    public void Validator_WellFormedXml_IsValid()
    {
        string path = SampleData.TempXmlPath();
        try
        {
            File.WriteAllText(path, "<?xml version=\"1.0\"?><LandXML version=\"1.2\" />");

            LandXmlOutputValidationResult result = LandXmlOutputValidator.Validate(path);

            Assert.True(result.Exists);
            Assert.True(result.IsWellFormedXml);
            Assert.True(result.IsValid);
            Assert.True(result.FileSizeBytes > 0);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void Validator_MalformedXml_NotWellFormed()
    {
        string path = SampleData.TempXmlPath();
        try
        {
            File.WriteAllText(path, "this is not xml at all");

            LandXmlOutputValidationResult result = LandXmlOutputValidator.Validate(path);

            Assert.True(result.Exists);
            Assert.False(result.IsWellFormedXml);
            Assert.False(result.IsValid);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void Validator_MissingFile_NotValid()
    {
        LandXmlOutputValidationResult result = LandXmlOutputValidator.Validate(SampleData.TempXmlPath());

        Assert.False(result.Exists);
        Assert.False(result.IsValid);
        Assert.Equal(0, result.FileSizeBytes);
    }

    [Fact]
    public void Report_Serialization_RoundTrips()
    {
        LandXmlExportData data = Data(alignments: 2, profiles: 3, surfaces: 1);
        LandXmlExportResult result = ExportedResult(exported: 6, skipped: 0, fileSize: 2048);
        LandXmlAnalysisResult analysis = LandXmlExportAnalyzer.Analyze(data, result);

        var report = new LandXmlExportReport
        {
            Summary = analysis.Summary,
            Statistics = analysis.Statistics,
            ExportedObjects = result.ExportedObjects,
            SkippedObjects = result.SkippedObjects,
            Recommendations = analysis.Recommendations,
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = "landxml.export",
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Elapsed = TimeSpan.FromMilliseconds(5),
                TotalSteps = 6,
                CompletedSteps = 6,
            },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        LandXmlExportReport? roundTripped =
            JsonSerializer.Deserialize<LandXmlExportReport>(json, SharedJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(report.Summary.Status, roundTripped!.Summary.Status);
        Assert.Equal(2048, roundTripped.Summary.FileSizeBytes);
        Assert.Equal(6, roundTripped.Statistics.TotalCollected);
        Assert.Equal(6, roundTripped.ExportedObjects.Count);
        Assert.Equal("Export completed successfully", Assert.Single(roundTripped.Recommendations).Title);
        Assert.Equal("landxml.export", roundTripped.Execution.WorkflowName);
        Assert.Equal(6, roundTripped.Execution.TotalSteps);
    }

    private static LandXmlExportData Data(int alignments, int profiles, int surfaces) => new()
    {
        OutputPath = @"C:\out\site.xml",
        IncludeAlignments = true,
        IncludeProfiles = true,
        IncludeSurfaces = true,
        AlignmentCount = alignments,
        ProfileCount = profiles,
        SurfaceCount = surfaces,
    };

    private static LandXmlExportResult ExportedResult(int exported, int skipped, long fileSize) => new()
    {
        Status = LandXmlExportStatus.Exported,
        OutputPath = @"C:\out\site.xml",
        FileSizeBytes = fileSize,
        ExportedObjects = Enumerable.Range(1, exported)
            .Select(i => new ExportedObject { Type = "Alignment", Name = $"AL{i}", Id = i })
            .ToArray(),
        SkippedObjects = Enumerable.Range(1, skipped)
            .Select(i => new SkippedObject { Type = "Corridor", Name = $"CR{i}", Id = i, Reason = "Unsupported." })
            .ToArray(),
        CompletedAtUtc = DateTimeOffset.UtcNow,
    };

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
