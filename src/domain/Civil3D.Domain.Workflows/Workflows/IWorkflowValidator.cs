using Civil3D.Domain.Commands;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// Validates a workflow before execution. A workflow may register any number of validators (all
/// are collected through dependency injection and run before any step side effect); a failure
/// maps to <c>E_VALIDATION_FAILED</c> on the wire. Reuses the command framework's
/// <see cref="ValidationResult"/> / <see cref="ValidationFailure"/> so there is a single
/// validation vocabulary across the platform.
/// </summary>
/// <typeparam name="TWorkflow">The workflow type.</typeparam>
public interface IWorkflowValidator<in TWorkflow>
    where TWorkflow : IWorkflow
{
    /// <summary>Validates the workflow.</summary>
    /// <param name="workflow">The workflow to validate.</param>
    ValidationResult Validate(TWorkflow workflow);
}
