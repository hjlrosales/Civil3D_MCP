using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Validation.Workflow;

/// <summary>
/// The five stages of the design-validation workflow. Steps resolve their domain services and the
/// validation engine from the workflow context (never Autodesk APIs), report progress and honour
/// cancellation between reads. The dispatcher's completion milestone is the sixth spec stage,
/// "Complete".
/// </summary>
internal sealed class ValidateInputStep : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<IValidationRule> rules = context.Services.GetServices<IValidationRule>().ToArray();
        if (rules.Count == 0)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "No validation rules are registered with the container.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, $"{rules.Count} rule(s) registered.");
        return Task.FromResult(WorkflowStepOutcome.Proceed($"{rules.Count} rule(s) registered."));
    }
}

/// <summary>Collects the active drawing snapshot, the lightweight statistics and every domain collection.</summary>
internal sealed class CollectDomainDataStep(DesignValidationWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Collect Domain Data";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IServiceProvider services = context.Services;

        var session = services.GetRequiredService<ICivil3DSession>();
        var statistics = services.GetRequiredService<IDrawingStatisticsService>();

        ActiveDrawing drawing = session.GetActiveDrawing()
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "No active drawing is available to validate.");
        state.Drawing = drawing;
        state.Statistics = statistics.GetStatistics(drawing, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

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

/// <summary>Runs every registered validation rule through the engine over the collected data.</summary>
internal sealed class ExecuteValidationRulesStep(DesignValidationWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Execute Validation Rules";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var engine = context.Services.GetRequiredService<IValidationEngine>();
        var data = new ValidationData
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

        var validationContext = new ValidationContext(
            context.CorrelationId, context.SessionId ?? string.Empty, cancellationToken, context.Logger);
        state.Execution = engine.ExecuteRules(data, validationContext);

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} executed {Rules} rule(s) and produced {Count} finding(s) "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Execution.RulesExecuted,
            state.Execution.Issues.Count, context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name, $"{state.Execution.Issues.Count} finding(s).");
        return Task.FromResult(WorkflowStepOutcome.Proceed($"{state.Execution.Issues.Count} finding(s)."));
    }
}

/// <summary>Aggregates the engine output into the report sections (summary, categories, recommendations).</summary>
internal sealed class AggregateResultsStep(DesignValidationWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Aggregate Results";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var execution = state.Execution
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Validation rules did not run.");
        var engine = context.Services.GetRequiredService<IValidationEngine>();
        IValidationResult result = engine.Aggregate(execution, state.ObjectCount());
        state.Result = result;

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} aggregated {Total} finding(s): {Critical} critical, {Errors} error, "
            + "{Warnings} warning, {Information} information (correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, result.Summary.TotalIssues, result.Summary.CriticalCount,
            result.Summary.ErrorCount, result.Summary.WarningCount, result.Summary.InformationCount,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name, $"{result.Summary.TotalIssues} finding(s) aggregated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Results aggregated."));
    }
}

/// <summary>Composes the final report from the collected data, engine output and execution summary.</summary>
internal sealed class GenerateReportStep(DesignValidationWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = state.Result
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Validation rules did not run.");
        ActiveDrawing drawing = state.Drawing
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Drawing information was not collected.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new DesignValidationReport
        {
            DrawingName = drawing.DrawingName,
            DrawingPath = drawing.DrawingPath,
            DrawingVersion = drawing.DrawingVersion,
            Civil3DVersion = drawing.Civil3DVersion,
            Statistics = state.Statistics ?? new DrawingStatistics(),
            Summary = result.Summary,
            Categories = result.Categories,
            Issues = result.Issues,
            Recommendations = result.Recommendations,
            Execution = new ValidationExecutionSummary
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
internal static class DesignValidationWorkflowStateExtensions
{
    /// <summary>The total number of domain objects collected so far.</summary>
    public static int ObjectCount(this DesignValidationWorkflowState state)
        => state.Alignments.Count + state.Surfaces.Count + state.Profiles.Count
           + state.Corridors.Count + state.PipeNetworks.Count + state.CogoPoints.Count
           + state.Styles.Count;
}
