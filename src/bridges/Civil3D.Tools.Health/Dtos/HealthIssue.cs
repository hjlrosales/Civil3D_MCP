namespace Civil3D.Tools.Health.Dtos;

/// <summary>
/// A single health finding. Each issue carries a stable machine-readable <see cref="Code"/>, a
/// severity, a category, a human-readable description and the guidance triad (reason, suggested
/// action, related object) plus optional secondary recommendations. Immutable and serializable.
/// </summary>
public sealed record HealthIssue
{
    /// <summary>Stable machine-readable finding code, for example <c>ORPHANED_CORRIDOR</c>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>The severity of the finding.</summary>
    public HealthSeverity Severity { get; init; }

    /// <summary>The category this finding belongs to (for example <c>Corridors</c>).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Human-readable description of the finding.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Why the finding matters.</summary>
    public string? Reason { get; init; }

    /// <summary>What the user should do about it.</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>The related object name, when the finding targets a specific object.</summary>
    public string? RelatedObject { get; init; }

    /// <summary>Optional secondary recommendations; an empty list when none apply.</summary>
    public IReadOnlyList<HealthRecommendation> Recommendations { get; init; } = Array.Empty<HealthRecommendation>();
}
