namespace Civil3D.Domain.Commands;

/// <summary>A single validation failure on a command input.</summary>
/// <param name="Field">The offending property name, or an empty string for command-level failures.</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record ValidationFailure(string Field, string Message);

/// <summary>The outcome of validating a command; valid when there are no failures.</summary>
public sealed record ValidationResult
{
    /// <summary>The validated result.</summary>
    public static ValidationResult Valid { get; } = new(Array.Empty<ValidationFailure>());

    /// <summary>The validation failures.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>True when the command passed validation.</summary>
    public bool IsValid => Failures.Count == 0;

    /// <summary>Creates a validation result from the given failures.</summary>
    /// <param name="failures">The failures; empty means valid.</param>
    public ValidationResult(IReadOnlyList<ValidationFailure> failures) => Failures = failures;

    /// <summary>Creates an invalid result.</summary>
    /// <param name="failures">The failures.</param>
    public static ValidationResult Invalid(params ValidationFailure[] failures) => new(failures);
}

/// <summary>
/// Validates a command before execution. A command may register any number of validators (all are
/// collected through dependency injection and run in the pipeline before any side effect); a
/// failure maps to <c>E_VALIDATION_FAILED</c> on the wire.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandValidator<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Validates the command.</summary>
    /// <param name="command">The command to validate.</param>
    ValidationResult Validate(TCommand command);
}
