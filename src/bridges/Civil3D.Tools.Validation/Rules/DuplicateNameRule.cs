using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds objects that share a name within the same collection (alignments, surfaces, profiles,
/// corridors and pipe networks). COGO points are excluded because they are identified by point
/// number, which the <c>duplicate-cogo-point-numbers</c> rule covers. Warning severity.
/// </summary>
public sealed class DuplicateNameRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "duplicate-names";

    /// <inheritdoc />
    public string Category => "Names";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        AddDuplicates(issues, data.Alignments, a => a.Name, "alignments", "ALIGNMENT");
        AddDuplicates(issues, data.Surfaces, s => s.Name, "surfaces", "SURFACE");
        AddDuplicates(issues, data.Profiles, p => p.Name, "profiles", "PROFILE");
        AddDuplicates(issues, data.Corridors, c => c.Name, "corridors", "CORRIDOR");
        AddDuplicates(issues, data.PipeNetworks, n => n.Name, "pipe networks", "PIPE_NETWORK");

        return issues;
    }

    private static void AddDuplicates<T>(
        List<ValidationIssue> issues, IReadOnlyList<T> items, Func<T, string> nameSelector,
        string kindLabel, string code)
    {
        foreach (string name in items
                     .GroupBy(nameSelector, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
        {
            issues.Add(new ValidationIssue
            {
                Code = $"DUPLICATE_{code}_NAME",
                Rule = "duplicate-names",
                Severity = ValidationSeverity.Warning,
                Category = "Names",
                Title = $"Duplicate {kindLabel} name '{name}'",
                Description = $"Multiple {kindLabel} share the name '{name}'.",
                SuggestedAction = "Rename the duplicate objects to unique names.",
                RelatedObject = name,
            });
        }
    }
}
