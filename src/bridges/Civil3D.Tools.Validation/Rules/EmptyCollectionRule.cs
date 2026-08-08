using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Flags domain collections that are unexpectedly empty. Information severity: an empty
/// collection is only a finding when the drawing otherwise contains Civil 3D content, so callers
/// can decide whether emptiness is expected for their workflow.
/// </summary>
public sealed class EmptyCollectionRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "empty-collections";

    /// <inheritdoc />
    public string Category => "Collections";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        // Only flag an empty collection when the drawing has at least some Civil 3D content;
        // a brand-new blank drawing legitimately has nothing yet.
        if (data.ObjectCount == 0)
        {
            return issues;
        }

        AddIfEmpty(issues, data.Alignments.Count, "alignments", "ALIGNMENTS");
        AddIfEmpty(issues, data.Surfaces.Count, "surfaces", "SURFACES");
        AddIfEmpty(issues, data.Profiles.Count, "profiles", "PROFILES");
        AddIfEmpty(issues, data.Corridors.Count, "corridors", "CORRIDORS");
        AddIfEmpty(issues, data.PipeNetworks.Count, "pipe networks", "PIPE_NETWORKS");
        AddIfEmpty(issues, data.CogoPoints.Count, "COGO points", "COGO_POINTS");
        AddIfEmpty(issues, data.Styles.Count, "styles", "STYLES");

        return issues;
    }

    private static void AddIfEmpty(
        List<ValidationIssue> issues, int count, string kindLabel, string code)
    {
        if (count != 0)
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Code = $"EMPTY_{code}",
            Rule = "empty-collections",
            Severity = ValidationSeverity.Information,
            Category = "Collections",
            Title = $"The drawing contains no {kindLabel}",
            Description = $"The drawing contains no {kindLabel}.",
            SuggestedAction = "Confirm the collection is expected to be empty; otherwise add content or ignore this finding.",
        });
    }
}
