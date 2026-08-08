namespace Civil3D.Domain.Corridors.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D corridor. Contains only serializable types;
/// the primary alignment is referenced by id. No geometry or assembly data.
/// </summary>
public sealed record CorridorInfo
{
    /// <summary>Stable numeric id derived from the corridor's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The corridor name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The corridor description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>Id of the corridor style, or <see langword="null"/> when not styled.</summary>
    public long? StyleId { get; init; }

    /// <summary>Id of the code set style, or <see langword="null"/> when not styled.</summary>
    public long? CodeSetStyleId { get; init; }

    /// <summary>Id of the primary baseline alignment, or <see langword="null"/> when the corridor has no baselines.</summary>
    public long? AlignmentId { get; init; }

    /// <summary>Number of baselines in the corridor.</summary>
    public int BaselineCount { get; init; }

    /// <summary>Number of corridor surfaces built on the corridor.</summary>
    public int CorridorSurfaceCount { get; init; }
}
