namespace Civil3D.Domain.Cogo.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D COGO point.
/// </summary>
public sealed record CogoPointInfo
{
    /// <summary>Stable numeric id derived from the point's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The point number.</summary>
    public uint PointNumber { get; init; }

    /// <summary>The point easting.</summary>
    public double Easting { get; init; }

    /// <summary>The point northing.</summary>
    public double Northing { get; init; }

    /// <summary>The point elevation.</summary>
    public double Elevation { get; init; }

    /// <summary>The full (expanded) point description, or <see langword="null"/> when empty.</summary>
    public string? FullDescription { get; init; }

    /// <summary>True when the point is locked against edits.</summary>
    public bool IsLocked { get; init; }
}
