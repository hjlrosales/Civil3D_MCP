using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds object-to-object references that fail to resolve: profiles and corridors referencing a
/// missing alignment, and alignments and corridors referencing a missing style. Error severity.
/// Only relationships already exposed by the domain DTOs are checked.
/// </summary>
public sealed class UnresolvedReferenceRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "unresolved-references";

    /// <inheritdoc />
    public string Category => "References";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        HashSet<long> alignmentIds = data.Alignments.Select(a => a.Id).ToHashSet();
        HashSet<long> styleIds = data.Styles.Select(s => s.Id).ToHashSet();

        // Profiles: AlignmentId is a plain long; 0 means "no owning alignment" (covered by the
        // profiles-without-alignment rule), any other value must resolve.
        foreach (ProfileInfo profile in data.Profiles)
        {
            if (profile.AlignmentId != 0 && !alignmentIds.Contains(profile.AlignmentId))
            {
                issues.Add(ReferenceIssue(
                    "UNRESOLVED_ALIGNMENT_REFERENCE",
                    $"Profile '{profile.Name}' references alignment id {profile.AlignmentId}, which does not exist.",
                    "Re-associate the profile with an existing alignment or remove it.",
                    profile.Name));
            }
        }

        foreach (CorridorInfo corridor in data.Corridors)
        {
            if (corridor.AlignmentId is { } alignmentId && !alignmentIds.Contains(alignmentId))
            {
                issues.Add(ReferenceIssue(
                    "UNRESOLVED_ALIGNMENT_REFERENCE",
                    $"Corridor '{corridor.Name}' references alignment id {alignmentId}, which does not exist.",
                    "Re-associate the corridor with an existing alignment or remove it.",
                    corridor.Name));
            }

            if (corridor.StyleId is { } corridorStyleId && !styleIds.Contains(corridorStyleId))
            {
                issues.Add(ReferenceIssue(
                    "UNRESOLVED_STYLE_REFERENCE",
                    $"Corridor '{corridor.Name}' references style id {corridorStyleId}, which does not exist.",
                    "Re-assign a valid corridor style.",
                    corridor.Name));
            }

            if (corridor.CodeSetStyleId is { } codeSetId && !styleIds.Contains(codeSetId))
            {
                issues.Add(ReferenceIssue(
                    "UNRESOLVED_CODE_SET_STYLE_REFERENCE",
                    $"Corridor '{corridor.Name}' references code set style id {codeSetId}, which does not exist.",
                    "Re-assign a valid code set style.",
                    corridor.Name));
            }
        }

        foreach (AlignmentInfo alignment in data.Alignments)
        {
            if (alignment.StyleId is { } styleId && !styleIds.Contains(styleId))
            {
                issues.Add(ReferenceIssue(
                    "UNRESOLVED_STYLE_REFERENCE",
                    $"Alignment '{alignment.Name}' references style id {styleId}, which does not exist.",
                    "Re-assign a valid alignment style.",
                    alignment.Name));
            }
        }

        return issues;
    }

    private static ValidationIssue ReferenceIssue(
        string code, string description, string suggestedAction, string relatedObject)
        => new()
        {
            Code = code,
            Rule = "unresolved-references",
            Severity = ValidationSeverity.Error,
            Category = "References",
            Title = code.Replace('_', ' '),
            Description = description,
            SuggestedAction = suggestedAction,
            RelatedObject = relatedObject,
        };
}
