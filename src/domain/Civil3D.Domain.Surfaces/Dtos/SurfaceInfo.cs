namespace Civil3D.Domain.Surfaces.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D surface, including cheap general properties
/// (point count and elevation range). Contains only serializable types; no Autodesk references.
/// </summary>
public sealed record SurfaceInfo
{
    /// <summary>Stable numeric id derived from the surface's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The surface name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The surface description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>The surface classification (TIN, grid, …).</summary>
    public SurfaceKind Kind { get; init; }

    /// <summary>Number of points in the surface definition.</summary>
    public int PointCount { get; init; }

    /// <summary>Minimum surface elevation.</summary>
    public double MinimumElevation { get; init; }

    /// <summary>Maximum surface elevation.</summary>
    public double MaximumElevation { get; init; }

    /// <summary>Mean surface elevation.</summary>
    public double MeanElevation { get; init; }
}
