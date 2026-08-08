namespace Civil3D.Domain.Workflows;

/// <summary>
/// Executes a workflow. Handlers are resolved by the dispatcher from the container by closed
/// generic type and receive their domain services through constructor injection; they run the
/// workflow's steps (typically via <see cref="WorkflowStepExecutor"/>) and produce the typed
/// result. Handlers never touch Autodesk APIs.
/// </summary>
/// <typeparam name="TWorkflow">The workflow type.</typeparam>
/// <typeparam name="TResult">The workflow result type.</typeparam>
public interface IWorkflowHandler<in TWorkflow, TResult>
    where TWorkflow : IWorkflow<TResult>
{
    /// <summary>Executes the workflow.</summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="context">Per-execution workflow context (progress, services, logging, configuration).</param>
    /// <param name="cancellationToken">Effective cancellation token (client cancellation and timeout linked).</param>
    Task<TResult> HandleAsync(TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Convenience base for workflow handlers: runs the workflow's steps in order (with milestone
/// progress reporting and step failure mapping) then asks the derived class to produce the
/// result. Keeps the step loop in one place so future workflows only implement
/// <see cref="ProduceResultAsync"/>.
/// </summary>
/// <typeparam name="TWorkflow">The workflow type.</typeparam>
/// <typeparam name="TResult">The workflow result type.</typeparam>
public abstract class WorkflowHandlerBase<TWorkflow, TResult> : IWorkflowHandler<TWorkflow, TResult>
    where TWorkflow : IWorkflow<TResult>
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(
        TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        await WorkflowStepExecutor.RunStepsAsync(workflow.Steps, context, cancellationToken).ConfigureAwait(false);
        return await ProduceResultAsync(workflow, context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Produces the workflow result after all steps completed.</summary>
    /// <param name="workflow">The completed workflow.</param>
    /// <param name="context">Per-execution workflow context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    protected abstract Task<TResult> ProduceResultAsync(
        TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken);
}
