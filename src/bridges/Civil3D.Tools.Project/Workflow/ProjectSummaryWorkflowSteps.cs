using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Project.Analysis;
using Civil3D.Tools.Project.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Project.Workflow;

/// <summary>
/// The five stages of the project-summary workflow. Steps resolve their domain services from the
/// workflow context (never Autodesk APIs), report progress and honour cancellation between reads.
/// </summary>
internal sealed class ValidateInputStep(ProjectSummaryOptions options) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (options.SmallScoreThreshold < 0
            || options.MediumScoreThreshold < 0
            || options.LargeScoreThreshold < 0
            || options.MaxNameListLength < 0
            || options.LargeDrawingEntityThreshold < 0)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The project summary analyzer options contain negative thresholds.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, "Input validated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Input validated."));
    }
}

/// <summary>Collects the active drawing snapshot and the lightweight drawing statistics.</summary>
internal sealed class CollectDrawingInformationStep(ProjectSummaryWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Collect Drawing Information";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = context.Services.GetRequiredService<ICivil3DSession>();
        var statistics = context.Services.GetRequiredService<IDrawingStatisticsService>();

        ActiveDrawing drawing = session.GetActiveDrawing()
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "No active drawing is available to inspect.");

        state.Drawing = drawing;
        state.Statistics = statistics.GetStatistics(drawing, cancellationToken);

        context.Progress.Report(context.Progress.PercentComplete, Name, $"Drawing '{drawing.DrawingName}' collected.");
        return Task.FromResult(WorkflowStepOutcome.Proceed($"Drawing '{drawing.DrawingName}' collected."));
    }
}

/// <summary>Materializes every domain collection through the existing read-only domain services.</summary>
internal sealed class CollectDomainObjectsStep(ProjectSummaryWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Collect Domain Objects";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IServiceProvider services = context.Services;

        state.Alignments = services.GetRequiredService<IAlignmentService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.Surfaces = services.GetRequiredService<ISurfaceService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.Profiles = services.GetRequiredService<IProfileService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.Corridors = services.GetRequiredService<ICorridorService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.PipeNetworks = services.GetRequiredService<IPipeService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.CogoPoints = services.GetRequiredService<ICogoService>().GetAll().Items;
        cancellationToken.ThrowIfCancellationRequested();
        state.Styles = services.GetRequiredService<IStyleService>().GetAll().Items;

        int objectCount = state.ObjectCount();
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} collected {Count} objects (correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, objectCount, context.CorrelationId, context.SessionId);
        context.Progress.Report(context.Progress.PercentComplete, Name, $"{objectCount} objects collected.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Domain objects collected."));
    }
}

/// <summary>Runs the pure <see cref="ProjectAnalyzer"/> over the collected data.</summary>
internal sealed class AnalyzeRelationshipsStep(ProjectSummaryWorkflowState state, ProjectSummaryOptions options) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Analyze Relationships";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = new ProjectData
        {
            Drawing = state.Drawing
                ?? throw new WorkflowException(
                    WorkflowErrorCode.InvalidParameters, "Drawing information was not collected."),
            Statistics = state.Statistics,
            Alignments = state.Alignments,
            Surfaces = state.Surfaces,
            Profiles = state.Profiles,
            Corridors = state.Corridors,
            PipeNetworks = state.PipeNetworks,
            CogoPoints = state.CogoPoints,
            Styles = state.Styles,
        };

        state.Analysis = ProjectAnalyzer.Analyze(data, options);
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} classified complexity {Classification} with {Count} recommendation(s) "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Analysis.Complexity.Classification,
            state.Analysis.Recommendations.Count, context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"{state.Analysis.Complexity.Classification} complexity, {state.Analysis.Recommendations.Count} recommendation(s).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Relationships analyzed."));
    }
}

/// <summary>Composes the final report from the collected data, analysis and execution summary.</summary>
internal sealed class GenerateSummaryStep(ProjectSummaryWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Summary";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = state.Analysis
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Analysis was not produced.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new ProjectSummaryReport
        {
            Overview = analysis.Overview,
            Inventory = analysis.Inventory,
            References = analysis.References,
            Complexity = analysis.Complexity,
            Statistics = analysis.Statistics,
            Recommendations = analysis.Recommendations,
            Execution = new WorkflowExecutionSummary
            {
                WorkflowName = context.WorkflowName,
                StartedAtUtc = context.StartedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                Elapsed = finishedAtUtc - context.StartedAtUtc,
                TotalSteps = totalSteps,
                CompletedSteps = totalSteps,
            },
        };

        context.Progress.Report(context.Progress.PercentComplete, Name, "Summary generated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Summary generated."));
    }
}

/// <summary>Counts the materialized domain objects held by the state.</summary>
internal static class ProjectSummaryWorkflowStateExtensions
{
    /// <summary>The total number of domain objects collected so far.</summary>
    public static int ObjectCount(this ProjectSummaryWorkflowState state)
        => state.Alignments.Count + state.Surfaces.Count + state.Profiles.Count
           + state.Corridors.Count + state.PipeNetworks.Count + state.CogoPoints.Count
           + state.Styles.Count;
}
