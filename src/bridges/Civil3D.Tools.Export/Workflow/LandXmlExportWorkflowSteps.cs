using System.Diagnostics;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Analysis;
using Civil3D.Tools.Export.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Export.Workflow;

/// <summary>
/// The six stages of the LandXML export workflow. Steps resolve their domain services and the
/// <see cref="ILandXmlExporter"/> from the workflow context (never Autodesk APIs), report
/// progress and honour cancellation between reads. The dispatcher's completion milestone is the
/// seventh spec stage, "Complete".
/// </summary>
internal sealed class ValidateInputStep(LandXmlExportRequest request) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Input";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "An output file path is required.");
        }

        if (!request.OutputPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The output file path must end in .xml.");
        }

        if (!request.IncludeAlignments && !request.IncludeProfiles && !request.IncludeSurfaces
            && !request.IncludeCorridors && !request.IncludePipeNetworks)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "At least one object type must be enabled for export.");
        }

        if (File.Exists(request.OutputPath) && !request.OverwriteExisting)
        {
            throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The output file already exists; set overwriteExisting to true to replace it.");
        }

        context.Progress.Report(context.Progress.PercentComplete, Name, "Input validated.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Input validated."));
    }
}

/// <summary>
/// Collects the object counts for every enabled type exactly once through the read-only domain
/// services.
/// </summary>
internal sealed class CollectExportDataStep(LandXmlExportWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Collect Export Data";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state.Request.IncludeAlignments)
        {
            state.AlignmentCount = context.Services.GetRequiredService<IAlignmentService>().Count();
        }
        if (state.Request.IncludeProfiles)
        {
            state.ProfileCount = context.Services.GetRequiredService<IProfileService>().Count();
        }
        if (state.Request.IncludeSurfaces)
        {
            state.SurfaceCount = context.Services.GetRequiredService<ISurfaceService>().Count();
        }
        if (state.Request.IncludeCorridors)
        {
            state.CorridorCount = context.Services.GetRequiredService<ICorridorService>().Count();
        }
        if (state.Request.IncludePipeNetworks)
        {
            state.PipeNetworkCount = context.Services.GetRequiredService<IPipeService>().Count();
        }
        cancellationToken.ThrowIfCancellationRequested();

        int total = state.AlignmentCount + state.ProfileCount + state.SurfaceCount
            + state.CorridorCount + state.PipeNetworkCount;

        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} collected {Total} object(s) (alignments {Alignments}, "
            + "profiles {Profiles}, surfaces {Surfaces}, corridors {Corridors}, pipe networks {Pipes}) "
            + "(correlation {CorrelationId}, session {SessionId}).",
            context.WorkflowName, Name, total, state.AlignmentCount, state.ProfileCount,
            state.SurfaceCount, state.CorridorCount, state.PipeNetworkCount,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"{total} object(s) collected.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Export data collected."));
    }
}

/// <summary>Composes the immutable exporter input from the request and the collected counts.</summary>
internal sealed class BuildExportOptionsStep(LandXmlExportWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Build Export Options";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = state.Request;
        state.ExportData = new LandXmlExportData
        {
            OutputPath = request.OutputPath,
            OverwriteExisting = request.OverwriteExisting,
            IncludeAlignments = request.IncludeAlignments,
            IncludeProfiles = request.IncludeProfiles,
            IncludeSurfaces = request.IncludeSurfaces,
            IncludeCorridors = request.IncludeCorridors,
            IncludePipeNetworks = request.IncludePipeNetworks,
            AlignmentCount = state.AlignmentCount,
            ProfileCount = state.ProfileCount,
            SurfaceCount = state.SurfaceCount,
            CorridorCount = state.CorridorCount,
            PipeNetworkCount = state.PipeNetworkCount,
        };

        context.Progress.Report(context.Progress.PercentComplete, Name, "Export options built.");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Export options built."));
    }
}

/// <summary>Runs the <see cref="ILandXmlExporter"/> over the prepared snapshot and times it.</summary>
internal sealed class ExecuteExportStep(LandXmlExportWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Execute Export";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = state.ExportData
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "Export options were not built.");
        var exporter = context.Services.GetRequiredService<ILandXmlExporter>();

        var timer = Stopwatch.StartNew();
        state.Result = exporter.Export(data);
        timer.Stop();

        var result = state.Result;
        context.Logger.LogInformation(
            "Workflow {Workflow} step {Step} finished in {Elapsed} ms with status {Status} writing "
            + "to {OutputPath}, exported {Exported} skipped {Skipped} (correlation {CorrelationId}, "
            + "session {SessionId}).",
            context.WorkflowName, Name, timer.ElapsedMilliseconds, result.Status,
            result.OutputPath, result.ExportedObjects.Count, result.SkippedObjects.Count,
            context.CorrelationId, context.SessionId);
        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            $"Export {result.Status} ({result.ExportedObjects.Count} exported, "
            + $"{result.SkippedObjects.Count} skipped).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Export executed."));
    }
}

/// <summary>
/// Validates the written file (exists, non-empty, well-formed XML) when the export completed;
/// skipped when the exporter reported not-supported.
/// </summary>
internal sealed class ValidateOutputStep(LandXmlExportWorkflowState state) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Validate Output";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = state.Result
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The export was not executed.");

        if (result.Status == LandXmlExportStatus.Exported)
        {
            state.Validation = LandXmlOutputValidator.Validate(result.OutputPath);
            if (!state.Validation.IsValid)
            {
                throw new WorkflowException(
                    WorkflowErrorCode.StepFailed,
                    "The export did not produce a valid LandXML file at the requested path.");
            }
        }

        context.Progress.Report(
            context.Progress.PercentComplete, Name,
            result.Status == LandXmlExportStatus.Exported
                ? $"Output validated ({state.Validation!.FileSizeBytes} bytes)."
                : "No file to validate (export not supported).");
        return Task.FromResult(WorkflowStepOutcome.Proceed("Output validated."));
    }
}

/// <summary>Composes the final report from the analysis output and execution summary.</summary>
internal sealed class GenerateReportStep(LandXmlExportWorkflowState state, int totalSteps) : IWorkflowStep
{
    /// <inheritdoc />
    public string Name => "Generate Report";

    /// <inheritdoc />
    public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = state.ExportData
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "Export options were not built.");
        var result = state.Result
            ?? throw new WorkflowException(
                WorkflowErrorCode.InvalidParameters,
                "The export was not executed.");

        LandXmlAnalysisResult analysis = LandXmlExportAnalyzer.Analyze(data, result);
        state.Analysis = analysis;

        DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;
        state.Report = new LandXmlExportReport
        {
            Summary = analysis.Summary,
            Statistics = analysis.Statistics,
            ExportedObjects = result.ExportedObjects,
            SkippedObjects = result.SkippedObjects,
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
