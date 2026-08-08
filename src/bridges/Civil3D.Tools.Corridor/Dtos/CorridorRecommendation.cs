namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// A recommendation derived purely from available corridor metrics, for example to review
/// corridors without generated surfaces. <c>RelatedCorridor</c> is null for drawing-level
/// recommendations.
/// </summary>
public sealed record CorridorRecommendation
{
    /// <summary>A concise title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Why the recommendation was produced.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the recommendation is.</summary>
    public CorridorSeverity Severity { get; init; }

    /// <summary>The action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;

    /// <summary>The corridor the recommendation relates to, when applicable.</summary>
    public string? RelatedCorridor { get; init; }
}
