using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Quantity.Analysis;
using Civil3D.Tools.Quantity.Dtos;

namespace Civil3D.Tools.Quantity.Workflow;

/// <summary>
/// The <c>quantity_takeoff_report</c> workflow: collects drawing and domain data through existing
/// domain services, runs the pure <see cref="QuantityCalculator"/> and generates the quantity
/// report. Read-only end to end. Steps resolve their dependencies from the workflow context; the
/// tool creates a fresh workflow instance per invocation.
/// </summary>
public sealed class QuantityTakeoffWorkflow : IWorkflow<QuantityTakeoffReport>
{
    /// <inheritdoc />
    public string Name => "quantity.takeoff.report";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.ReadOnly;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal QuantityTakeoffWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    public QuantityTakeoffWorkflow()
    {
        State = new QuantityTakeoffWorkflowState();

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(),
            new CollectDrawingInformationStep(State),
            new CollectDomainDataStep(State),
            new CalculateQuantitiesStep(State),
            new AggregateResultsStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
