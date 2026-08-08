using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Analysis;

/// <summary>
/// Pure, Autodesk-free analysis engine for the LandXML export report. Turns the collected object
/// counts and the exporter output into the report pieces: the export summary (written or
/// not-supported), per-type statistics and recommendations derived only from the actual outcome.
/// The class holds no state; every method is static, so it is trivially testable.
/// </summary>
public static class LandXmlExportAnalyzer
{
    /// <summary>Analyzes the export outcome for the report.</summary>
    /// <param name="data">The validated request options and collected object counts.</param>
    /// <param name="result">The exporter output.</param>
    public static LandXmlAnalysisResult Analyze(LandXmlExportData data, LandXmlExportResult result)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(result);

        // Only the enabled object types count as considered for export.
        int alignmentCount = data.IncludeAlignments ? data.AlignmentCount : 0;
        int profileCount = data.IncludeProfiles ? data.ProfileCount : 0;
        int surfaceCount = data.IncludeSurfaces ? data.SurfaceCount : 0;
        int corridorCount = data.IncludeCorridors ? data.CorridorCount : 0;
        int pipeNetworkCount = data.IncludePipeNetworks ? data.PipeNetworkCount : 0;
        int totalCollected = alignmentCount + profileCount + surfaceCount + corridorCount + pipeNetworkCount;

        bool exported = result.Status == LandXmlExportStatus.Exported;

        var summary = new ExportSummary
        {
            Status = exported ? "Exported" : "Not Supported",
            OutputPath = data.OutputPath,
            FileSizeBytes = exported ? result.FileSizeBytes : 0,
            ExportedCount = exported ? result.ExportedObjects.Count : 0,
            SkippedCount = exported ? result.SkippedObjects.Count : 0,
            NotSupportedReason = exported ? null : result.Reason,
        };

        var statistics = new ExportStatistics
        {
            AlignmentCount = alignmentCount,
            ProfileCount = profileCount,
            SurfaceCount = surfaceCount,
            CorridorCount = corridorCount,
            PipeNetworkCount = pipeNetworkCount,
            TotalCollected = totalCollected,
            ExportedCount = summary.ExportedCount,
            SkippedCount = summary.SkippedCount,
        };

        return new LandXmlAnalysisResult
        {
            Summary = summary,
            Statistics = statistics,
            Recommendations = BuildRecommendations(exported, summary, totalCollected),
        };
    }

    private static IReadOnlyList<ExportRecommendation> BuildRecommendations(
        bool exported, ExportSummary summary, int totalCollected)
    {
        var recommendations = new List<ExportRecommendation>();

        if (!exported)
        {
            recommendations.Add(new ExportRecommendation
            {
                Title = "Export not supported by installed API",
                Description = summary.NotSupportedReason ?? "No export path is available.",
                Severity = ExportSeverity.Warning,
                SuggestedAction = "Run the export interactively in Civil 3D or wait for an "
                    + "Autodesk-backed exporter.",
            });
        }
        else if (totalCollected == 0)
        {
            recommendations.Add(new ExportRecommendation
            {
                Title = "No objects to export",
                Description = "The requested object types are not present in the drawing.",
                Severity = ExportSeverity.Information,
                SuggestedAction = "Enable an object type that exists in the drawing and retry.",
            });
        }
        else if (summary.SkippedCount > 0)
        {
            recommendations.Add(new ExportRecommendation
            {
                Title = "Review skipped objects",
                Description = $"{summary.SkippedCount} object(s) were skipped because the installed "
                    + "API does not support exporting them.",
                Severity = ExportSeverity.Warning,
                SuggestedAction = "Export the skipped types interactively or omit them from the "
                    + "request.",
            });
        }
        else
        {
            recommendations.Add(new ExportRecommendation
            {
                Title = "Export completed successfully",
                Description = $"The LandXML file was written to '{summary.OutputPath}' "
                    + $"({summary.ExportedCount} object(s)).",
                Severity = ExportSeverity.Information,
                SuggestedAction = "Verify the file in the consuming application.",
            });
        }

        return recommendations;
    }
}
