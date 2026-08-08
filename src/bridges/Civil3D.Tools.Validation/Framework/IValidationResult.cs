using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// The consolidated outcome of a validation run: the findings, per-category roll-ups, the
/// severity/rule summary and the top-level recommendations. Immutable; produced by the
/// <see cref="IValidationEngine"/>.
/// </summary>
public interface IValidationResult
{
    /// <summary>The findings, ordered by severity then code.</summary>
    IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>Per-category severity roll-ups.</summary>
    IReadOnlyList<ValidationCategory> Categories { get; }

    /// <summary>The severity and rule roll-up of the findings.</summary>
    ValidationSummary Summary { get; }

    /// <summary>Top-level recommendations summarising the state of the drawing.</summary>
    IReadOnlyList<ValidationRecommendation> Recommendations { get; }
}
