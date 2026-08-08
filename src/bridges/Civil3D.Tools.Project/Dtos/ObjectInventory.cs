namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The object inventory of the drawing: domain object counts, drawing symbol-table and space
/// counts, plus capped name lists for the principal feature objects. No Autodesk types.
/// </summary>
public sealed record ObjectInventory
{
    /// <summary>The number of alignments.</summary>
    public int AlignmentCount { get; init; }

    /// <summary>The number of profiles.</summary>
    public int ProfileCount { get; init; }

    /// <summary>The number of surfaces.</summary>
    public int SurfaceCount { get; init; }

    /// <summary>The number of corridors.</summary>
    public int CorridorCount { get; init; }

    /// <summary>The number of pipe networks.</summary>
    public int PipeNetworkCount { get; init; }

    /// <summary>The total number of pipes across all networks.</summary>
    public int PipeCount { get; init; }

    /// <summary>The total number of structures across all networks.</summary>
    public int StructureCount { get; init; }

    /// <summary>The number of COGO points.</summary>
    public int CogoPointCount { get; init; }

    /// <summary>The number of styles.</summary>
    public int StyleCount { get; init; }

    /// <summary>The number of entries in the layer table.</summary>
    public int LayerCount { get; init; }

    /// <summary>The number of entries in the block table.</summary>
    public int BlockCount { get; init; }

    /// <summary>The number of external references (xrefs).</summary>
    public int XRefCount { get; init; }

    /// <summary>The total number of entities in model and paper space.</summary>
    public int EntityCount { get; init; }

    /// <summary>The number of entities in model space.</summary>
    public int ModelSpaceEntityCount { get; init; }

    /// <summary>The number of entities in all paper space layouts.</summary>
    public int PaperSpaceEntityCount { get; init; }

    /// <summary>The number of viewport objects.</summary>
    public int ViewportCount { get; init; }

    /// <summary>The number of text styles.</summary>
    public int TextStyleCount { get; init; }

    /// <summary>The number of dimension styles.</summary>
    public int DimensionStyleCount { get; init; }

    /// <summary>The number of linetypes.</summary>
    public int LinetypeCount { get; init; }

    /// <summary>The alignment names (capped at the configured maximum).</summary>
    public IReadOnlyList<string> AlignmentNames { get; init; } = Array.Empty<string>();

    /// <summary>The surface names (capped at the configured maximum).</summary>
    public IReadOnlyList<string> SurfaceNames { get; init; } = Array.Empty<string>();

    /// <summary>The corridor names (capped at the configured maximum).</summary>
    public IReadOnlyList<string> CorridorNames { get; init; } = Array.Empty<string>();

    /// <summary>The pipe network names (capped at the configured maximum).</summary>
    public IReadOnlyList<string> PipeNetworkNames { get; init; } = Array.Empty<string>();

    /// <summary>True when a name list was truncated because it exceeded the configured maximum.</summary>
    public bool NamesTruncated { get; init; }
}
