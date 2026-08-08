using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Workflow;

/// <summary>
/// The <c>design_validation_report</c> workflow: collects drawing and domain data through existing
/// domain services, runs every registered validation rule through the
/// <see cref="Framework.IValidationEngine"/> and generates the report. Read-only end to end. Steps
/// resolve their dependencies from the workflow context; the tool creates a fresh workflow
/// instance per invocation.
/// </summary>
public sealed class DesignValidationWorkflow : IWorkflow<DesignValidationReport>
{
    /// <inheritdoc />
    public string Name => "design.validation.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal DesignValidationWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    public DesignValidationWorkflow()
    {
        State = new DesignValidationWorkflowState();

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(),
            new CollectDomainDataStep(State),
            new ExecuteValidationRulesStep(State),
            new AggregateResultsStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
