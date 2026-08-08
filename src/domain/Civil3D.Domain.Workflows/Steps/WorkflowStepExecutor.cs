using Civil3D.Domain.Errors;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// Runs the ordered steps of a workflow with milestone progress reporting (10–90%), cooperative
/// cancellation between steps and standard failure mapping: <c>DomainException</c> passes
/// through unchanged (its stable code maps to a protocol error in the tool layer),
/// <c>WorkflowException</c> passes through, and any other failure becomes
/// <see cref="WorkflowErrorCode.StepFailed"/>. A step that returns <c>Continue=false</c> stops
/// the run; the workflow still completes successfully.
/// </summary>
public static class WorkflowStepExecutor
{
    /// <summary>Runs the steps in order.</summary>
    /// <param name="steps">The steps to run.</param>
    /// <param name="context">Per-execution workflow context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    public static async Task RunStepsAsync(
        IEnumerable<IWorkflowStep> steps, IWorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(context);

        IWorkflowStep[] ordered = steps.ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IWorkflowStep step = ordered[i];
            int percent = 10 + (int)((double)i / ordered.Length * 80);
            context.Progress.Report(percent, step.Name, "Running");

            WorkflowStepOutcome outcome;
            try
            {
                outcome = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DomainException)
            {
                throw; // Stable domain code; mapped by the tool layer, not re-wrapped.
            }
            catch (WorkflowException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WorkflowException(
                    WorkflowErrorCode.StepFailed,
                    $"Workflow step '{step.Name}' failed.",
                    ex);
            }

            context.Progress.Report(percent, step.Name, outcome.Message);
            if (!outcome.Continue)
            {
                break;
            }
        }

        context.Progress.Report(90, "Steps complete");
    }
}
