namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// A recommendation derived purely from calculated values (volumes and surface data), for
/// example to balance earthwork or verify surface quality before construction.
/// </summary>
public sealed record CutFillRecommendation
{
    /// <summary>A concise title, for example <c>Significant net export</c>.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Why the recommendation was produced.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the recommendation is.</summary>
    public CutFillSeverity Severity { get; init; }

    /// <summary>The action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;
}
