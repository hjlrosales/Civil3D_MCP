namespace Civil3D.Tools.Validation.Dtos;

/// <summary>
/// A recommended action surfaced by the validation report. Top-level recommendations summarise
/// the state of the drawing; individual findings carry their own guidance. Contains only
/// serializable types; no Autodesk references.
/// </summary>
public sealed record ValidationRecommendation
{
    /// <summary>A concise title of the recommendation.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Human-readable description of the recommendation.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The severity of the finding(s) driving this recommendation.</summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>The concrete action to take.</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>The related object name, when the recommendation targets a specific object.</summary>
    public string? RelatedObject { get; init; }
}
