namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// The <c>export_landxml</c> result: a structured, read-only report of a LandXML export
/// attempt. Combines the export summary (written or not-supported), per-type statistics, the
/// exported and skipped object lists, recommendations and the execution summary. Immutable and
/// Autodesk-free.
/// </summary>
public sealed record LandXmlExportReport
{
    /// <summary>The headline export summary.</summary>
    public ExportSummary Summary { get; init; } = new();

    /// <summary>Per-type object counts and export totals.</summary>
    public ExportStatistics Statistics { get; init; } = new();

    /// <summary>Objects written into the file.</summary>
    public IReadOnlyList<ExportedObject> ExportedObjects { get; init; } = Array.Empty<ExportedObject>();

    /// <summary>Objects the exporter could not write, with reasons.</summary>
    public IReadOnlyList<SkippedObject> SkippedObjects { get; init; } = Array.Empty<SkippedObject>();

    /// <summary>Recommendations derived from the outcome.</summary>
    public IReadOnlyList<ExportRecommendation> Recommendations { get; init; } = Array.Empty<ExportRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
