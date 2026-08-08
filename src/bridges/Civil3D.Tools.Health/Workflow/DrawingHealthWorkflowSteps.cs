using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Health.Analysis;
using Civil3D.Tools.Health.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Health.Workflow;

/// <summary>
/// The five stages of the drawing-health workflow. Steps resolve their domain services from the
/// workflow context (never Autodesk APIs), report progress and honour cancellation between reads.
/// </summary>
internal sealed class ValidateInputStep(HealthAnalyzerOptions options) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (options.LargeDrawingEntityThreshold < 0
            || options.LargeModelSpaceEntityThreshold < 0
            || options.LargeSurfacePointThreshold < 0
            || options.LargeCogoPointThreshold < 0)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The health report analyzer options contain negative thresholds.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, "Input validated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Input validated."));
    }
}

/// <summary>Collects the active drawing snapshot and the lightweight drawing statistics.</summary>
internal sealed class CollectDrawingInformationStep(DrawingHealthWorkflowState state) : IWorkflowStep
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
internal sealed class CollectDomainDataStep(DrawingHealthWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Collect Domain Data";

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
        return Task.FromResult(WorkflowStepOutcome.Proceed("Domain data collected."));
    }
}

/// <summary>Runs the pure <see cref="HealthAnalyzer"/> over the collected data.</summary>
internal sealed class AnalyzeResultsStep(DrawingHealthWorkflowState state, HealthAnalyzerOptions options) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Analyze Results";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = new HealthData
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

        state.Analysis = HealthAnalyzer.Analyze(data, options);
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} produced {Count} findings (correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Analysis.Issues.Count, context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name, $"{state.Analysis.Issues.Count} findings.");
        return Task.FromResult(WorkflowStepOutcome.Proceed($"{state.Analysis.Issues.Count} findings."));
    }
}

/// <summary>Composes the final report from the collected data, analysis and execution summary.</summary>
internal sealed class GenerateReportStep(DrawingHealthWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = state.Analysis
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Analysis was not produced.");
        ActiveDrawing drawing = state.Drawing
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Drawing information was not collected.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new DrawingHealthReport
        {
            DrawingName = drawing.DrawingName,
            DrawingPath = drawing.DrawingPath,
            DrawingVersion = drawing.DrawingVersion,
            Civil3DVersion = drawing.Civil3DVersion,
            IsModified = drawing.IsModified,
            IsReadOnly = drawing.IsReadOnly,
            CurrentLayout = drawing.CurrentLayout,
            DatabaseFingerprint = drawing.DatabaseFingerprint,
            Statistics = state.Statistics ?? new DrawingStatistics(),
            Health = analysis.Statistics,
            Categories = analysis.Categories,
            Issues = analysis.Issues,
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

        context.Progress.Report(context.Progress.PercentComplete, Name, "Report generated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Report generated."));
    }
}

/// <summary>Counts the materialized domain objects held by the state.</summary>
internal static class DrawingHealthWorkflowStateExtensions
{
    /// <summary>The total number of domain objects collected so far.</summary>
    public static int ObjectCount(this DrawingHealthWorkflowState state)
        => state.Alignments.Count + state.Surfaces.Count + state.Profiles.Count
           + state.Corridors.Count + state.PipeNetworks.Count + state.CogoPoints.Count
           + state.Styles.Count;
}
