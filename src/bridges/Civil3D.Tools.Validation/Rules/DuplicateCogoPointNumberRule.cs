using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds COGO points that share a point number. Point numbers are the primary key of a point
/// set, so duplicates are ambiguous. Warning severity.
/// </summary>
public sealed class DuplicateCogoPointNumberRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "duplicate-cogo-point-numbers";

    /// <inheritdoc />
    public string Category => "COGO Points";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        foreach (uint number in data.CogoPoints
                     .GroupBy(p => p.PointNumber)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
        {
            issues.Add(new ValidationIssue
            {
                Code = "DUPLICATE_COGO_POINT_NUMBER",
                Rule = "duplicate-cogo-point-numbers",
                Severity = ValidationSeverity.Warning,
                Category = Category,
                Title = $"Duplicate COGO point number {number}",
                Description = $"Multiple COGO points share the point number {number}.",
                SuggestedAction = "Renumber the duplicate points so every point number is unique.",
                RelatedObject = number.ToString(),
            });
        }

        return issues;
    }
}
