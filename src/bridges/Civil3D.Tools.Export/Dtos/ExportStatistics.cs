namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// Aggregate object counts for the export run: how many of each type the drawing contains (the
/// collected counts), how many were exported and how many were skipped.
/// </summary>
public sealed record ExportStatistics
{
    /// <summary>Alignments present in the drawing (collected).</summary>
    public int AlignmentCount { get; init; }

    /// <summary>Profiles present in the drawing (collected).</summary>
    public int ProfileCount { get; init; }

    /// <summary>Surfaces present in the drawing (collected).</summary>
    public int SurfaceCount { get; init; }

    /// <summary>Corridors present in the drawing (collected).</summary>
    public int CorridorCount { get; init; }

    /// <summary>Pipe networks present in the drawing (collected).</summary>
    public int PipeNetworkCount { get; init; }

    /// <summary>Total objects considered for export.</summary>
    public int TotalCollected { get; init; }

    /// <summary>Objects written to the file.</summary>
    public int ExportedCount { get; init; }

    /// <summary>Objects skipped by the exporter.</summary>
    public int SkippedCount { get; init; }
}
