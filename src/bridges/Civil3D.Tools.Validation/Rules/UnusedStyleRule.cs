using Civil3D.Domain.Styles.Dtos;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds alignment and corridor styles that are not referenced by any inspected object. Only
/// object kinds whose referencing objects are exposed by the domain DTOs can be checked for
/// usage; other style kinds are ignored. Information severity.
/// </summary>
public sealed class UnusedStyleRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "unused-styles";

    /// <inheritdoc />
    public string Category => "Styles";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var referencedAlignmentStyles = data.Alignments.Select(a => a.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCorridorStyles = data.Corridors.Select(c => c.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCodeSetStyles = data.Corridors.Select(c => c.CodeSetStyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();

        var issues = new List<ValidationIssue>();
        foreach (StyleInfo style in data.Styles)
        {
            bool unused = style.Kind switch
            {
                StyleKind.Alignment => !referencedAlignmentStyles.Contains(style.Id),
                StyleKind.Corridor => !referencedCorridorStyles.Contains(style.Id)
                                      && !referencedCodeSetStyles.Contains(style.Id),
                _ => false,
            };

            if (unused)
            {
                issues.Add(new ValidationIssue
                {
                    Code = "UNUSED_STYLE",
                    Rule = "unused-styles",
                    Severity = ValidationSeverity.Information,
                    Category = Category,
                    Title = $"Style '{style.Name}' is not referenced",
                    Description = $"Style '{style.Name}' is not referenced by any inspected object.",
                    SuggestedAction = "Remove the style if it is no longer needed.",
                    RelatedObject = style.Name,
                });
            }
        }

        return issues;
    }
}
