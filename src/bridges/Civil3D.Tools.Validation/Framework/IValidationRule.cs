using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// A single, independently registered design validation rule. Rules are pure consumers of the
/// materialized <see cref="ValidationData"/> snapshot — they never touch Autodesk APIs — and are
/// composed by the <see cref="IValidationEngine"/> into a consolidated report. Implementations
/// must be stateless and safely executable in any order.
/// </summary>
public interface IValidationRule
{
    /// <summary>Stable machine-readable rule name, for example <c>duplicate-names</c>.</summary>
    string Name { get; }

    /// <summary>The category findings produced by this rule belong to (for example <c>Alignments</c>).</summary>
    string Category { get; }

    /// <summary>Evaluates the rule against the materialized drawing and domain data.</summary>
    /// <param name="data">The materialized snapshot of the active drawing and its domain objects.</param>
    /// <param name="context">Correlation/session identity, logger and cancellation.</param>
    /// <returns>The findings produced by this rule; an empty list when the rule passes.</returns>
    IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context);
}
