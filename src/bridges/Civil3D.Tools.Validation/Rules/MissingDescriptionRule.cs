using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds objects without a description across every discipline that exposes one (alignments,
/// surfaces, profiles, corridors, pipe networks and COGO points). Information severity.
/// </summary>
public sealed class MissingDescriptionRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "missing-descriptions";

    /// <inheritdoc />
    public string Category => "Metadata";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        AddMissing(issues, data.Alignments, a => a.Name, a => a.Description, "Alignment", "ALIGNMENT");
        AddMissing(issues, data.Surfaces, s => s.Name, s => s.Description, "Surface", "SURFACE");
        AddMissing(issues, data.Profiles, p => p.Name, p => p.Description, "Profile", "PROFILE");
        AddMissing(issues, data.Corridors, c => c.Name, c => c.Description, "Corridor", "CORRIDOR");
        AddMissing(issues, data.PipeNetworks, n => n.Name, n => n.Description, "Pipe network", "PIPE_NETWORK");
        AddMissing(issues, data.CogoPoints, p => p.PointNumber.ToString(), p => p.FullDescription,
            "COGO point", "COGO_POINT");

        return issues;
    }

    private static void AddMissing<T>(
        List<ValidationIssue> issues, IReadOnlyList<T> items, Func<T, string> nameSelector,
        Func<T, string?> descriptionSelector, string kindLabel, string code)
    {
        foreach (T item in items)
        {
            if (string.IsNullOrWhiteSpace(descriptionSelector(item)))
            {
                string name = nameSelector(item);
                issues.Add(new ValidationIssue
                {
                    Code = $"MISSING_{code}_DESCRIPTION",
                    Rule = "missing-descriptions",
                    Severity = ValidationSeverity.Information,
                    Category = "Metadata",
                    Title = $"{kindLabel} '{name}' has no description",
                    Description = $"{kindLabel} '{name}' has no description.",
                    SuggestedAction = "Add a short description to the object.",
                    RelatedObject = name,
                });
            }
        }
    }
}
