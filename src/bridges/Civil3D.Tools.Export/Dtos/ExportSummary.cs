namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// The headline export result: whether the file was written (or reported as not supported),
/// where it was written, its size and how many objects were exported and skipped.
/// </summary>
public sealed record ExportSummary
{
    /// <summary>Overall status: <c>Exported</c> or <c>Not Supported</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The output file path.</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Size of the written file in bytes; 0 when not supported.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>Objects written to the file.</summary>
    public int ExportedCount { get; init; }

    /// <summary>Objects the exporter could not write.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Reason for a not-supported result; null when the export completed.</summary>
    public string? NotSupportedReason { get; init; }
}
