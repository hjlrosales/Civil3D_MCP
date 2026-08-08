namespace Civil3D.Domain.Profiles.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D profile (centerline or existing ground).
/// Contains only serializable types; the owning alignment is referenced by id.
/// </summary>
public sealed record ProfileInfo
{
    /// <summary>Stable numeric id derived from the profile's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The profile name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The profile description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>Id of the alignment the profile belongs to.</summary>
    public long AlignmentId { get; init; }

    /// <summary>The Autodesk profile type name (for example <c>Layout</c> or <c>ExistingGround</c>).</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>The total length of the profile.</summary>
    public double Length { get; init; }

    /// <summary>The station at the start of the profile.</summary>
    public double StartingStation { get; init; }

    /// <summary>The station at the end of the profile.</summary>
    public double EndingStation { get; init; }

    /// <summary>The minimum elevation along the profile.</summary>
    public double MinimumElevation { get; init; }

    /// <summary>The maximum elevation along the profile.</summary>
    public double MaximumElevation { get; init; }
}
