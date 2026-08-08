namespace Civil3D.Domain.Styles.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D style (labeling/display configuration).
/// </summary>
public sealed record StyleInfo
{
    /// <summary>Stable numeric id derived from the style's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The style name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The style description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>The style collection this style belongs to.</summary>
    public StyleKind Kind { get; init; }
}
