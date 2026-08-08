namespace Civil3D.Domain.Workflows;

/// <summary>
/// Executes a workflow through the full pipeline: validation, permission check, timeout-linked
/// execution with progress reporting, structured logging, correlation/session propagation and
/// domain events. Handlers and validators are resolved through dependency injection. Throws
/// <see cref="WorkflowException"/> on failure (and passes <c>DomainException</c> through); the
/// tool layer maps codes to protocol errors.
/// </summary>
public interface IWorkflowDispatcher
{
    /// <summary>Dispatches a workflow for execution.</summary>
    /// <typeparam name="TWorkflow">The workflow type (registered handler required).</typeparam>
    /// <typeparam name="TResult">The workflow result type.</typeparam>
    /// <param name="workflow">The workflow instance.</param>
    /// <param name="context">Per-execution workflow context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    Task<WorkflowResult<TResult>> DispatchAsync<TWorkflow, TResult>(
        TWorkflow workflow,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        where TWorkflow : class, IWorkflow<TResult>;
}
