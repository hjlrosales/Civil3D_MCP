namespace Civil3D.Tools.Drawing.Dtos;

/// <summary>
/// The immutable result of <c>drawing_summary</c>: fast symbol-table and space counts of the active
/// drawing. Serialized to the wire by the dispatcher; never mutated after creation.
/// </summary>
public sealed record DrawingSummaryDto
{
    /// <summary>Number of entries in the layer table.</summary>
    public int LayerCount { get; init; }

    /// <summary>Number of entries in the block table (includes model space, paper space and all blocks).</summary>
    public int BlockCount { get; init; }

    /// <summary>Number of block table records that are external references (xrefs).</summary>
    public int XRefCount { get; init; }

    /// <summary>Total entities in model space and all paper space layouts.</summary>
    public int EntityCount { get; init; }

    /// <summary>Number of entities in model space.</summary>
    public int ModelSpaceEntityCount { get; init; }

    /// <summary>Number of entities in all paper space layouts combined.</summary>
    public int PaperSpaceEntityCount { get; init; }

    /// <summary>Number of viewport objects in all paper space layouts.</summary>
    public int ViewportCount { get; init; }

    /// <summary>Number of entries in the text style table.</summary>
    public int TextStyleCount { get; init; }

    /// <summary>Number of entries in the dimension style table.</summary>
    public int DimensionStyleCount { get; init; }

    /// <summary>Number of entries in the linetype table.</summary>
    public int LinetypeCount { get; init; }

    /// <summary>Number of entries in the registered application table.</summary>
    public int RegisteredApplicationCount { get; init; }

    /// <summary>Number of dictionaries under the named objects dictionary.</summary>
    public int DictionaryCount { get; init; }

    /// <summary>Approximate on-disk size of the drawing file in bytes; 0 when the file is unsaved or unavailable.</summary>
    public long ApproximateDrawingSizeBytes { get; init; }
}
