namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// A recommendation derived purely from the compared metrics, for example to review a large
/// point-count difference before running volume calculations.
/// </summary>
public sealed record ComparisonRecommendation
{
    /// <summary>A concise title, for example <c>Large point-count difference</c>.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Why the recommendation was produced.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the recommendation is.</summary>
    public ComparisonSeverity Severity { get; init; }

    /// <summary>The action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;

    /// <summary>The surface the recommendation relates to, when applicable.</summary>
    public string? RelatedSurface { get; init; }
}
