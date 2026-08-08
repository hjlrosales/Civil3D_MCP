using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Corridor.Analysis;
using Civil3D.Tools.Corridor.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Corridor.Workflow;

/// <summary>
/// The five stages of the corridor-analysis workflow. Steps resolve their domain services from
/// the workflow context (never Autodesk APIs), report progress and honour cancellation between
/// reads. The dispatcher's completion milestone is the sixth spec stage, "Complete".
/// </summary>
internal sealed class ValidateInputStep(CorridorAnalysisRequest request) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.CorridorId is <= 0)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The corridor id must be positive when supplied.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, "Input validated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Input validated."));
    }
}

/// <summary>
/// Loads the corridors to analyze exactly once through the read-only corridor service: the
/// requested corridor when an id is supplied, otherwise every corridor in the drawing.
/// </summary>
internal sealed class LoadCorridorDataStep(CorridorWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Load Corridor Data";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var corridors = context.Services.GetRequiredService<ICorridorService>();

        if (state.Request.CorridorId is long id)
        {
            CorridorInfo? corridor = corridors.GetById(id);
            cancellationToken.ThrowIfCancellationRequested();

            state.Corridors = corridor is null
                ? throw new DomainException(
                    DomainErrorCode.EntityNotFound,
                    $"No corridor with id {id} was found.")
                : new[] { corridor };
        }
        else
        {
            state.Corridors = corridors.GetAll().Items;
            cancellationToken.ThrowIfCancellationRequested();
        }

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} loaded {Count} corridor(s) for corridor id {CorridorId} "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Corridors.Count, state.Request.CorridorId,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"{state.Corridors.Count} corridor(s) loaded.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Corridor data loaded."));
    }
}

/// <summary>Runs the pure <see cref="CorridorAnalyzer"/> over the loaded corridors.</summary>
internal sealed class AnalyzeCorridorsStep(CorridorWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Analyze Corridors";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        state.Analysis = CorridorAnalyzer.Analyze(
            state.Corridors,
            options: null,
            includeStatistics: state.Request.IncludeStatistics);

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} produced verdict {Verdict} with {Count} issue(s) "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Analysis.Verdict, state.Analysis.Issues.Count,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"Verdict: {state.Analysis.Verdict} ({state.Analysis.Issues.Count} issue(s)).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Corridors analyzed."));
    }
}

/// <summary>Builds the recommendations from the available metrics when enabled.</summary>
internal sealed class GenerateRecommendationsStep(CorridorWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Recommendations";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = state.Analysis
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The corridor analysis was not produced.");

        if (state.Request.IncludeRecommendations)
        {
            state.Analysis = analysis with
            {
                Recommendations = CorridorAnalyzer.BuildRecommendations(state.Corridors),
            };
        }

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} generated {Count} recommendation(s) "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Analysis.Recommendations.Count,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"{state.Analysis.Recommendations.Count} recommendation(s).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Recommendations generated."));
    }
}

/// <summary>Composes the final report from the analysis output and execution summary.</summary>
internal sealed class GenerateReportStep(CorridorWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = state.Analysis
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The corridor analysis was not produced.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new CorridorAnalysisReport
        {
            Verdict = analysis.Verdict,
            Corridors = analysis.Corridors,
            Statistics = analysis.Statistics,
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
