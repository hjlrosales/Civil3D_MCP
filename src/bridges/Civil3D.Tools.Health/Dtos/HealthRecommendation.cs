namespace Civil3D.Tools.Health.Dtos;

/// <summary>
/// A recommended action surfaced by the health report. Top-level recommendations summarise the
/// state of the drawing; individual findings carry their own <see cref="HealthIssue"/> guidance.
/// Contains only serializable types; no Autodesk references.
/// </summary>
public sealed record HealthRecommendation
{
    /// <summary>A concise description of the recommendation.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Why this recommendation exists.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>The concrete action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;

    /// <summary>The related object name, when the recommendation targets a specific object.</summary>
    public string? RelatedObject { get; init; }
}
