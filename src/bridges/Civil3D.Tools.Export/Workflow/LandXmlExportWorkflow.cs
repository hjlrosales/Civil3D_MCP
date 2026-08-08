using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Analysis;
using Civil3D.Tools.Export.Dtos;

namespace Civil3D.Tools.Export.Workflow;

/// <summary>
/// The <c>export_landxml</c> workflow: validates the request, collects the object counts once
/// through the read-only domain services, builds the export snapshot, runs the
/// <see cref="ILandXmlExporter"/> (never the Civil 3D export APIs directly), validates the
/// written file and composes the export report. Creates an external file only; the drawing is
/// never modified. Steps resolve their dependencies from the workflow context; the tool creates
/// a fresh workflow instance per invocation. Requires <see cref="CommandPermission.Export"/>.
/// </summary>
public sealed class LandXmlExportWorkflow : IWorkflow<LandXmlExportReport>
{
    /// <inheritdoc />
    public string Name => "landxml.export";

    /// <inheritdoc />
    public CommandPermission RequiredPermission => CommandPermission.Export;

    /// <inheritdoc />
    public TimeSpan? Timeout => null; // The dispatcher applies its default timeout.

    /// <inheritdoc />
    public IReadOnlyList<IWorkflowStep> Steps { get; }

    /// <summary>The per-execution shared state written by the steps.</summary>
    internal LandXmlExportWorkflowState State { get; }

    /// <summary>Creates the workflow with its steps and shared state.</summary>
    /// <param name="request">The validated LandXML export request.</param>
    public LandXmlExportWorkflow(LandXmlExportRequest request)
    {
        State = new LandXmlExportWorkflowState { Request = request };

        var steps = new List<IWorkflowStep>
        {
            new ValidateInputStep(request),
            new CollectExportDataStep(State),
            new BuildExportOptionsStep(State),
            new ExecuteExportStep(State),
            new ValidateOutputStep(State),
        };

        // The report step needs the final step count; +1 accounts for itself.
        steps.Add(new GenerateReportStep(State, totalSteps: steps.Count + 1));
        Steps = steps;
    }
}
