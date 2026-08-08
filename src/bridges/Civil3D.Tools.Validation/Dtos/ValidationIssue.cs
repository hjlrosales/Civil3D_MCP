using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Dtos;

/// <summary>
/// A single validation finding. Each issue carries a stable machine-readable <see cref="Code"/>,
/// the originating rule, a severity, a category and the guidance fields (title, description,
/// suggested action, related object). Immutable and serializable; no Autodesk types.
/// </summary>
public sealed record ValidationIssue : IValidationIssue
{
    /// <summary>Stable machine-readable finding code, for example <c>UNRESOLVED_ALIGNMENT_REFERENCE</c>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>The rule name that produced this finding (matches <see cref="Framework.IValidationRule.Name"/>).</summary>
    public string Rule { get; init; } = string.Empty;

    /// <summary>The severity of the finding.</summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>The category this finding belongs to (for example <c>Profiles</c>).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>A concise title of the finding.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Human-readable description of the finding.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>What the user should do about it.</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>The related object name, when the finding targets a specific object.</summary>
    public string? RelatedObject { get; init; }
}
