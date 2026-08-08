namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// A single recommendation produced by the project summary. Immutable and serializable.
/// </summary>
public sealed record ProjectRecommendation
{
    /// <summary>A concise title, for example <c>Audit broken references</c>.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What the recommendation is about.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the recommendation is.</summary>
    public RecommendationPriority Priority { get; init; }

    /// <summary>The concrete action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;
}
