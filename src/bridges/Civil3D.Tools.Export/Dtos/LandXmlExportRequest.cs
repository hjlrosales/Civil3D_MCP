namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// Input for <c>export_landxml</c>: where to write the LandXML file, which object types to
/// include, and whether an existing file may be overwritten. Alignments, profiles and surfaces
/// are included by default; corridors and pipe networks default to off because support for them
/// depends on the installed API. Immutable and Autodesk-free.
/// </summary>
public sealed record LandXmlExportRequest
{
    /// <summary>The full output file path (required; must end in <c>.xml</c>).</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Include alignments when true (default).</summary>
    public bool IncludeAlignments { get; init; } = true;

    /// <summary>Include profiles when true (default).</summary>
    public bool IncludeProfiles { get; init; } = true;

    /// <summary>Include surfaces when true (default).</summary>
    public bool IncludeSurfaces { get; init; } = true;

    /// <summary>Include corridors when true (default false; support-dependent).</summary>
    public bool IncludeCorridors { get; init; }

    /// <summary>Include pipe networks when true (default false; support-dependent).</summary>
    public bool IncludePipeNetworks { get; init; }

    /// <summary>Allow replacing an existing file at <see cref="OutputPath"/> (default false).</summary>
    public bool OverwriteExisting { get; init; }
}
