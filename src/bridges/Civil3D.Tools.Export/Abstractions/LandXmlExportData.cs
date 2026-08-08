using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Abstractions;

/// <summary>
/// Immutable snapshot a <see cref="ILandXmlExporter"/> consumes: the validated request options
/// plus the object counts collected once from the domain services. The exporter decides which of
/// the included types it can actually write and reports the rest as skipped.
/// </summary>
public sealed record LandXmlExportData
{
    /// <summary>The output file path (validated).</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Whether an existing file at the output path may be replaced.</summary>
    public bool OverwriteExisting { get; init; }

    /// <summary>Include alignments when true.</summary>
    public bool IncludeAlignments { get; init; }

    /// <summary>Include profiles when true.</summary>
    public bool IncludeProfiles { get; init; }

    /// <summary>Include surfaces when true.</summary>
    public bool IncludeSurfaces { get; init; }

    /// <summary>Include corridors when true (support-dependent).</summary>
    public bool IncludeCorridors { get; init; }

    /// <summary>Include pipe networks when true (support-dependent).</summary>
    public bool IncludePipeNetworks { get; init; }

    /// <summary>Alignments present in the drawing.</summary>
    public int AlignmentCount { get; init; }

    /// <summary>Profiles present in the drawing.</summary>
    public int ProfileCount { get; init; }

    /// <summary>Surfaces present in the drawing.</summary>
    public int SurfaceCount { get; init; }

    /// <summary>Corridors present in the drawing.</summary>
    public int CorridorCount { get; init; }

    /// <summary>Pipe networks present in the drawing.</summary>
    public int PipeNetworkCount { get; init; }
}
