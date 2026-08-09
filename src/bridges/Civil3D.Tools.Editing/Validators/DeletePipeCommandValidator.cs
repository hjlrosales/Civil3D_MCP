using Civil3D.Domain.Commands;
using Civil3D.Tools.Editing.Commands;

namespace Civil3D.Tools.Editing.Validators;

/// <summary>
/// Structural rules for <see cref="DeletePipeCommand"/>, checked before any transaction: the
/// pipe id must be positive. Existence of the pipe and acceptance of the deletion by Civil 3D
/// are enforced later, inside the write transaction, by the delete-pipe service and repository.
/// </summary>
public sealed class DeletePipeCommandValidator : ICommandValidator<DeletePipeCommand>
{
    /// <inheritdoc />
    public ValidationResult Validate(DeletePipeCommand command)
    {
        var failures = new List<ValidationFailure>();

        if (command.PipeId <= 0)
        {
            failures.Add(new ValidationFailure(nameof(command.PipeId), "The pipe id must be greater than zero."));
        }

        return failures.Count == 0 ? ValidationResult.Valid : ValidationResult.Invalid(failures.ToArray());
    }
}
