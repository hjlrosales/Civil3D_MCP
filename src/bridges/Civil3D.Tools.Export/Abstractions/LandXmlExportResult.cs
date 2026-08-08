using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Abstractions;

/// <summary>
/// The exporter output: the written file's location and size, the objects exported and skipped,
/// or a structured not-supported result with the reason.
/// </summary>
public sealed record LandXmlExportResult
{
    /// <summary>Whether the export completed or is not supported.</summary>
    public LandXmlExportStatus Status { get; init; }

    /// <summary>Reason for a not-supported result; null when the export completed.</summary>
    public string? Reason { get; init; }

    /// <summary>The output file path.</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Size of the written file in bytes; 0 when not supported.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>Objects written into the file.</summary>
    public IReadOnlyList<ExportedObject> ExportedObjects { get; init; } = Array.Empty<ExportedObject>();

    /// <summary>Objects the exporter could not write, with reasons.</summary>
    public IReadOnlyList<SkippedObject> SkippedObjects { get; init; } = Array.Empty<SkippedObject>();

    /// <summary>UTC timestamp when the export finished; null when not supported.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
