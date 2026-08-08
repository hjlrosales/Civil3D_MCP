using Civil3D.Domain.Errors;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Surface.Analysis;
using Civil3D.Tools.Surface.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Surface.Workflow;

/// <summary>
/// The five stages of the surface-comparison workflow. Steps resolve their domain services from
/// the workflow context (never Autodesk APIs), report progress and honour cancellation between
/// reads. The dispatcher's completion milestone is the sixth spec stage, "Complete".
/// </summary>
internal sealed class ValidateInputStep(SurfaceComparisonRequest request) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ExistingSurfaceId <= 0 || request.ProposedSurfaceId <= 0)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "Both surface ids must be positive.");
        }

        if (request.ExistingSurfaceId == request.ProposedSurfaceId)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The existing and proposed surface ids must differ.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, "Input validated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Input validated."));
    }
}

/// <summary>Loads both surfaces exactly once through the read-only surface service.</summary>
internal sealed class LoadSurfaceMetadataStep(SurfaceComparisonWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Load Surface Metadata";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var surfaces = context.Services.GetRequiredService<ISurfaceService>();

        SurfaceInfo? existing = surfaces.GetById(state.Request.ExistingSurfaceId);
        cancellationToken.ThrowIfCancellationRequested();
        SurfaceInfo? proposed = surfaces.GetById(state.Request.ProposedSurfaceId);

        state.ExistingSurface = existing ?? throw new DomainException(
            DomainErrorCode.EntityNotFound,
            $"No surface with id {state.Request.ExistingSurfaceId} was found.");
        state.ProposedSurface = proposed ?? throw new DomainException(
            DomainErrorCode.EntityNotFound,
            $"No surface with id {state.Request.ProposedSurfaceId} was found.");

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} loaded surfaces {ExistingId} and {ProposedId} "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.ExistingSurface.Id, state.ProposedSurface.Id,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"'{state.ExistingSurface.Name}' vs '{state.ProposedSurface.Name}' loaded.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Surface metadata loaded."));
    }
}

/// <summary>Builds the immutable comparison snapshot the engine consumes.</summary>
internal sealed class LoadComparisonDataStep(SurfaceComparisonWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Load Comparison Data";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = state.ExistingSurface
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Existing surface was not loaded.");
        var proposed = state.ProposedSurface
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Proposed surface was not loaded.");

        state.Data = new SurfaceComparisonData
        {
            ExistingSurface = existing,
            ProposedSurface = proposed,
            IncludeStatistics = state.Request.IncludeStatistics,
            IncludeRecommendations = state.Request.IncludeRecommendations,
        };

        context.Progress.Report(context.Progress.PercentComplete, Name, "Comparison data prepared.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Comparison data prepared."));
    }
}

/// <summary>Runs the pure <see cref="SurfaceComparer"/> over the loaded snapshot.</summary>
internal sealed class AnalyzeDifferencesStep(SurfaceComparisonWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Analyze Differences";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = state.Data
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Comparison data was not prepared.");

        state.Result = SurfaceComparer.Compare(data);
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} compared {Metrics} metric(s), found {Differences} "
            + "difference(s) and produced {Count} recommendation(s) (correlation {CorrelationId}, "
            + "session {SessionId}).",
            context.WorkflowName, Name, state.Result.Summary.MetricCount,
            state.Result.Summary.DifferenceCount, state.Result.Summary.RecommendationCount,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"{state.Result.Summary.DifferenceCount} difference(s) found.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Differences analyzed."));
    }
}

/// <summary>Composes the final report from the comparison output and execution summary.</summary>
internal sealed class GenerateReportStep(SurfaceComparisonWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = state.Result
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Comparison was not produced.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new SurfaceComparisonReport
        {
            Summary = result.Summary,
            Metrics = result.Metrics,
            Differences = result.Differences,
            Statistics = result.Statistics,
            Recommendations = result.Recommendations,
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
