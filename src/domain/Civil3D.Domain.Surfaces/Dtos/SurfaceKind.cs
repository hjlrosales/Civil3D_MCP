namespace Civil3D.Domain.Surfaces.Dtos;

/// <summary>
/// Stable, serializable classification of a surface, determined by the concrete Autodesk type
/// (<c>TinSurface</c>, <c>GridSurface</c>, …). Unknown types map to <see cref="Other"/>.
/// </summary>
public enum SurfaceKind
{
    /// <summary>Triangulated irregular network surface.</summary>
    Tin,

    /// <summary>Grid (lattice) surface.</summary>
    Grid,

    /// <summary>TIN volume surface.</summary>
    TinVolume,

    /// <summary>Grid volume surface.</summary>
    GridVolume,

    /// <summary>Any surface type not covered by the values above.</summary>
    Other,
}
