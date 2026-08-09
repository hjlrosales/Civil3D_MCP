using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Commands;

namespace Civil3D.Tools.Editing.Validators;

/// <summary>
/// Structural rules for <see cref="UpdatePipeCommand"/>, checked before any transaction: the
/// pipe id must be positive and at least one change must be requested, with every requested
/// change finite and strictly positive. Existence of the pipe and acceptance of the changes by
/// Civil 3D are enforced later, inside the write transaction, by the update-pipe service and
/// repository.
/// </summary>
public sealed class UpdatePipeCommandValidator : ICommandValidator<UpdatePipeCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(UpdatePipeCommand command)
    {
        var failures = new List<ValidationFailure>();

        if (command.PipeId <= 0)
        {
            failures.Add(new ValidationFailure(nameof(command.PipeId), "The pipe id must be greater than zero."));
        }

        if (command.ElevationMeters is not { } elevation
            && command.LengthMeters is not { } length
            && command.DiameterMm is not { } diameter)
        {
            failures.Add(new ValidationFailure(
                nameof(command.ElevationMeters),
                "At least one of elevation, length or diameter must be provided to update the pipe."));
        }

        if (command.ElevationMeters is { } e && !double.IsFinite(e))
        {
            failures.Add(new ValidationFailure(nameof(command.ElevationMeters), "The elevation must be a finite number."));
        }

        if (command.LengthMeters is { } l && (!double.IsFinite(l) || l <= 0))
        {
            failures.Add(new ValidationFailure(nameof(command.LengthMeters), "The length must be greater than zero."));
        }

        if (command.DiameterMm is { } d && (!double.IsFinite(d) || d <= 0))
        {
            failures.Add(new ValidationFailure(nameof(command.DiameterMm), "The diameter must be greater than zero."));
        }

        return failures.Count == 0 ? ValidationResult.Valid : ValidationResult.Invalid(failures.ToArray());
    }
}
