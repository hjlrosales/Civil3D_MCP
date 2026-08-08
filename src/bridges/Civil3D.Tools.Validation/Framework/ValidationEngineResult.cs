using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// Immutable <see cref="IValidationResult"/> produced by <see cref="ValidationEngine.ExecuteRules"/>.
/// </summary>
/// <param name="Issues">The findings, ordered by severity then code.</param>
/// <param name="Categories">Per-category severity roll-ups.</param>
/// <param name="Summary">The severity and rule roll-up of the findings.</param>
/// <param name="Recommendations">Top-level recommendations.</param>
public sealed record ValidationEngineResult(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<ValidationCategory> Categories,
    ValidationSummary Summary,
    IReadOnlyList<ValidationRecommendation> Recommendations) : IValidationResult;
