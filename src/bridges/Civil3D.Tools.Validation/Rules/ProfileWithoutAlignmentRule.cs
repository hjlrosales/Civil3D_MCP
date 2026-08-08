using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds profiles with no owning alignment. A profile whose <c>AlignmentId</c> is 0 (the DTO's
/// "none" value) has no parent alignment; profiles whose alignment id is non-zero but missing
/// are covered by the <c>unresolved-references</c> rule. Warning severity.
/// </summary>
public sealed class ProfileWithoutAlignmentRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "profiles-without-alignment";

    /// <inheritdoc />
    public string Category => "Profiles";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        foreach (var profile in data.Profiles.Where(p => p.AlignmentId == 0))
        {
            issues.Add(new ValidationIssue
            {
                Code = "PROFILE_WITHOUT_ALIGNMENT",
                Rule = "profiles-without-alignment",
                Severity = ValidationSeverity.Warning,
                Category = Category,
                Title = $"Profile '{profile.Name}' has no owning alignment",
                Description = $"Profile '{profile.Name}' is not associated with any alignment.",
                SuggestedAction = "Associate the profile with an alignment or remove it.",
                RelatedObject = profile.Name,
            });
        }

        return issues;
    }
}
