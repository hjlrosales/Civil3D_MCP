namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// A recommendation derived from the export outcome, for example to review skipped objects or
/// to run the export interactively when the installed API does not support it.
/// </summary>
public sealed record ExportRecommendation
{
    /// <summary>A concise title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Why the recommendation was produced.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the recommendation is.</summary>
    public ExportSeverity Severity { get; init; }

    /// <summary>The action to take.</summary>
    public string SuggestedAction { get; init; } = string.Empty;
}
