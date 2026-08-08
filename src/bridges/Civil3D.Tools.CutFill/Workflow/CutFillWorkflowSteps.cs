using System.Diagnostics;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Analysis;
using Civil3D.Tools.CutFill.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.CutFill.Workflow;

/// <summary>
/// The six stages of the cut/fill workflow. Steps resolve their domain services and the
/// <see cref="ICutFillCalculator"/> from the workflow context (never Autodesk APIs), report
/// progress and honour cancellation between reads. The dispatcher's completion milestone is the
/// seventh spec stage, "Complete".
/// </summary>
internal sealed class ValidateInputStep(CutFillRequest request) : IWorkflowStep
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
internal sealed class LoadSurfacesStep(CutFillWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Load Surfaces";

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
        return Task.FromResult(WorkflowStepOutcome.Proceed("Surfaces loaded."));
    }
}

/// <summary>Builds the immutable calculation snapshot the calculator consumes.</summary>
internal sealed class PrepareCalculationStep(CutFillWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Prepare Calculation";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = state.ExistingSurface
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Existing surface was not loaded.");
        var proposed = state.ProposedSurface
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Proposed surface was not loaded.");

        state.Data = new CutFillCalculationData
        {
            ExistingSurface = existing,
            ProposedSurface = proposed,
        };

        context.Progress.Report(context.Progress.PercentComplete, Name, "Calculation prepared.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Calculation prepared."));
    }
}

/// <summary>Runs the <see cref="ICutFillCalculator"/> over the prepared snapshot and times it.</summary>
internal sealed class ExecuteCalculationStep(CutFillWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Execute Calculation";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = state.Data
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Calculation was not prepared.");
        var calculator = context.Services.GetRequiredService<ICutFillCalculator>();

        var timer = Stopwatch.StartNew();
        state.Calculation = calculator.Calculate(data);
        timer.Stop();

        var result = state.Calculation;
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} finished in {Elapsed} ms with status {Status}, "
            + "cut {Cut} fill {Fill} net {Net} (correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, timer.ElapsedMilliseconds, result.Status,
            result.CutVolume, result.FillVolume, result.NetVolume,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"Calculation {result.Status} (cut {result.CutVolume:0.###}, fill {result.FillVolume:0.###}).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Calculation executed."));
    }
}

/// <summary>Runs the pure <see cref="CutFillAnalyzer"/> over the calculator output.</summary>
internal sealed class AnalyzeResultsStep(CutFillWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Analyze Results";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = state.Data
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Calculation was not prepared.");
        var calculation = state.Calculation
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Calculation was not executed.");

        state.Analysis = CutFillAnalyzer.Analyze(
            data, calculation,
            state.Request.IncludeStatistics,
            state.Request.IncludeRecommendations);

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} produced verdict {Verdict} with {Count} "
            + "recommendation(s) (correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, state.Analysis.Summary.Verdict,
            state.Analysis.Recommendations.Count, context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"Verdict: {state.Analysis.Summary.Verdict}.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Results analyzed."));
    }
}

/// <summary>Composes the final report from the analysis output and execution summary.</summary>
internal sealed class GenerateReportStep(CutFillWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = state.Analysis
            ?? throw new WorkflowException(WorkflowErrorCode.InvalidParameters, "Analysis was not produced.");

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new CutFillReport
        {
            Summary = analysis.Summary,
            Differences = analysis.Differences,
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

        context.Progress.Report(context.Progress.PercentComplete, Name, "Report generated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Report generated."));
    }
}
