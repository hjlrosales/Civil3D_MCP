namespace Civil3D.Domain.Workflows;

/// <summary>
/// A reusable stage of a workflow. Steps receive the <see cref="IWorkflowContext"/> (progress,
/// logger, services, configuration) and the effective cancellation token; they return an outcome
/// that either continues the workflow or stops it early. Steps never touch Autodesk APIs.
/// </summary>
public interface IWorkflowStep
{
    /// <summary>The step name (used in progress milestones and logs).</summary>
    string Name { get; }

    /// <summary>Executes the step.</summary>
    /// <param name="context">Per-execution workflow context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken);
}

/// <summary>The outcome of one workflow step: continue with the next step or stop the workflow.</summary>
/// <param name="Continue">True to continue with the next step; false stops the workflow (successfully).</param>
/// <param name="Message">Optional message reported through progress.</param>
public sealed record WorkflowStepOutcome(bool Continue, string? Message = null)
{
    /// <summary>Continues to the next step.</summary>
    /// <param name="message">Optional progress message.</param>
    public static WorkflowStepOutcome Proceed(string? message = null) => new(Continue: true, message);

    /// <summary>Stops the workflow after this step (the workflow still completes successfully).</summary>
    /// <param name="message">Optional progress message.</param>
    public static WorkflowStepOutcome Stop(string? message = null) => new(Continue: false, message);
}
