using System.Text.RegularExpressions;
using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Commands;

namespace Civil3D.Tools.Editing.Validators;

/// <summary>
/// Shared structural rules for Civil 3D object names, used by every rename validator so the
/// disciplines cannot drift. All checks are pure string validation; existence and uniqueness are
/// enforced by the rename service inside the write transaction.
/// </summary>
public static partial class NameRules
{
    /// <summary>Maximum length of an object name.</summary>
    public const int MaxLength = 64;

    /// <summary>Characters allowed in an object name (letters, digits, space, and common separators).</summary>
    [GeneratedRegex(@"^[\w ._()\-']+$")]
    private static partial Regex AllowedCharacters();

    /// <summary>Validates the structural rules of a proposed name.</summary>
    /// <param name="field">The property name reported on failure (for example "NewName").</param>
    /// <param name="newName">The proposed name.</param>
    public static ValidationResult Validate(string field, string? newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return ValidationResult.Invalid(new ValidationFailure(field, "The name must not be empty."));
        }

        if (newName.Length > MaxLength)
        {
            return ValidationResult.Invalid(new ValidationFailure(
                field, $"The name must be at most {MaxLength} characters."));
        }

        if (!AllowedCharacters().IsMatch(newName))
        {
            return ValidationResult.Invalid(new ValidationFailure(
                field, "The name contains unsupported characters."));
        }

        return ValidationResult.Valid;
    }
}

/// <summary>Validates <see cref="RenameAlignmentCommand"/> names before execution.</summary>
public sealed class RenameAlignmentCommandValidator : ICommandValidator<RenameAlignmentCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(RenameAlignmentCommand command)
        => NameRules.Validate(nameof(command.NewName), command.NewName);
}

/// <summary>Validates <see cref="RenameSurfaceCommand"/> names before execution.</summary>
public sealed class RenameSurfaceCommandValidator : ICommandValidator<RenameSurfaceCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(RenameSurfaceCommand command)
        => NameRules.Validate(nameof(command.NewName), command.NewName);
}
