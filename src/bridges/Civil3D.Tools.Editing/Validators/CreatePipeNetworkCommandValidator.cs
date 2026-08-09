using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Commands;

namespace Civil3D.Tools.Editing.Validators;

/// <summary>
/// Structural rules for <see cref="CreatePipeNetworkCommand"/>, checked before any transaction:
/// the network name must be present and each requested material must be non-blank. Uniqueness of
/// the network name and catalog resolution of the materials are enforced later, inside the write
/// transaction, by the create-pipe-network service and repository.
/// </summary>
public sealed class CreatePipeNetworkCommandValidator : ICommandValidator<CreatePipeNetworkCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(CreatePipeNetworkCommand command)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(command.NetworkName))
        {
            failures.Add(new ValidationFailure(nameof(command.NetworkName), "The network name must not be empty."));
        }

        foreach (string material in command.Materials)
        {
            if (string.IsNullOrWhiteSpace(material))
            {
                failures.Add(new ValidationFailure(
                    nameof(command.Materials), "Each pipe material must not be blank."));
                break;
            }
        }

        foreach (double diameter in command.SizesMm)
        {
            if (diameter <= 0 || double.IsNaN(diameter) || double.IsInfinity(diameter))
            {
                failures.Add(new ValidationFailure(
                    nameof(command.SizesMm), "Each requested size must be a positive diameter in millimetres."));
                break;
            }
        }

        return failures.Count == 0 ? ValidationResult.Valid : ValidationResult.Invalid(failures.ToArray());
    }
}
