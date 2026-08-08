using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Commands;

namespace Civil3D.Tools.Editing.Validators;

/// <summary>
/// Structural rules for <see cref="CreatePipeCommand"/>, checked before any transaction: the
/// network and part family match must be present, the diameter and horizontal length must be
/// positive, and the pipe must actually be horizontal (equal start/end elevation). Existence of
/// the network and resolution of the part family are enforced later, inside the write
/// transaction, by the create-pipe service and repository.
/// </summary>
public sealed class CreatePipeCommandValidator : ICommandValidator<CreatePipeCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(CreatePipeCommand command)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(command.NetworkName))
        {
            failures.Add(new ValidationFailure(nameof(command.NetworkName), "The network name must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(command.PartFamilyMatch))
        {
            failures.Add(new ValidationFailure(
                nameof(command.PartFamilyMatch), "A material, or an explicit part family match, must be specified."));
        }

        if (!double.IsFinite(command.DiameterMm) || command.DiameterMm <= 0)
        {
            failures.Add(new ValidationFailure(nameof(command.DiameterMm), "The diameter must be greater than zero."));
        }

        double lengthMeters = Math.Sqrt(
            Math.Pow(command.EndEasting - command.StartEasting, 2) +
            Math.Pow(command.EndNorthing - command.StartNorthing, 2));
        if (!double.IsFinite(lengthMeters) || lengthMeters <= 0)
        {
            failures.Add(new ValidationFailure(nameof(command.LengthMeters), "The pipe length must be greater than zero."));
        }

        if (!double.IsFinite(command.StartElevation))
        {
            failures.Add(new ValidationFailure(nameof(command.StartElevation), "The start elevation must be a finite number."));
        }

        return failures.Count == 0 ? ValidationResult.Valid : ValidationResult.Invalid(failures.ToArray());
    }
}
