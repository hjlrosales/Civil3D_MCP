namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// The engineering discipline a quantity line item belongs to. The report groups items by
/// category and rolls up per-category totals so callers can see both the raw item list and the
/// summary.
/// </summary>
public enum QuantityCategory
{
    /// <summary>Civil 3D alignments (centerlines, offsets, …).</summary>
    Alignments = 0,

    /// <summary>Profiles (existing ground, layout).</summary>
    Profiles = 1,

    /// <summary>Surfaces (TIN, grid, …).</summary>
    Surfaces = 2,

    /// <summary>Corridors and their assemblies.</summary>
    Corridors = 3,

    /// <summary>Pipe networks, including their pipes and structures.</summary>
    Pipes = 4,

    /// <summary>COGO (survey) points.</summary>
    CogoPoints = 5,

    /// <summary>Styles and labelling configuration.</summary>
    Styles = 6,

    /// <summary>Drawing-level tables and counts (layers, blocks, xrefs, entities, …).</summary>
    Drawing = 7,
}
