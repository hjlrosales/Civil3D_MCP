namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// A single validation finding produced by an <see cref="IValidationRule"/>. Implemented by the
/// serializable <c>ValidationIssue</c> DTO; the interface keeps rules, the engine and consumers
/// decoupled from the concrete record.
/// </summary>
public interface IValidationIssue
{
    /// <summary>Stable machine-readable finding code, for example <c>UNRESOLVED_ALIGNMENT_REFERENCE</c>.</summary>
    string Code { get; }

    /// <summary>The rule name that produced this finding.</summary>
    string Rule { get; }

    /// <summary>The severity of the finding.</summary>
    Dtos.ValidationSeverity Severity { get; }

    /// <summary>The category this finding belongs to.</summary>
    string Category { get; }

    /// <summary>A concise title of the finding.</summary>
    string Title { get; }

    /// <summary>Human-readable description of the finding.</summary>
    string Description { get; }

    /// <summary>What the user should do about it.</summary>
    string? SuggestedAction { get; }

    /// <summary>The related object name, when the finding targets a specific object.</summary>
    string? RelatedObject { get; }
}
