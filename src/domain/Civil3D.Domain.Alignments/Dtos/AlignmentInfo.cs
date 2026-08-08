namespace Civil3D.Domain.Alignments.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D alignment. Contains only serializable types and
/// uses stable numeric ids instead of Autodesk object references. Produced by the
/// <c>AutodeskAlignmentDataSource</c> and never mutated after creation.
/// </summary>
public sealed record AlignmentInfo
{
    /// <summary>Stable numeric id derived from the alignment's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The alignment name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The alignment description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>The alignment classification (centerline, offset, …).</summary>
    public AlignmentKind Kind { get; init; }

    /// <summary>The total length of the alignment.</summary>
    public double Length { get; init; }

    /// <summary>The station at the start of the alignment.</summary>
    public double StartingStation { get; init; }

    /// <summary>The station at the end of the alignment.</summary>
    public double EndingStation { get; init; }

    /// <summary>Id of the site owning the alignment, or <see langword="null"/> for siteless alignments.</summary>
    public long? SiteId { get; init; }

    /// <summary>Id of the alignment style, or <see langword="null"/> when not styled.</summary>
    public long? StyleId { get; init; }
}
