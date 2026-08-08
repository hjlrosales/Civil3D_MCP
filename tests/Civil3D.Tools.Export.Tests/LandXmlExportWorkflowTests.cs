using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Civil3D.Tools.Export.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Export.Tests.ExportHarness;
using static Civil3D.Tools.Export.Tests.TestDoubles;

namespace Civil3D.Tools.Export.Tests;

/// <summary>
/// The LandXML export workflow end to end through the real dispatcher: orchestration over the
/// counting services and fake exporter, report composition, progress milestones, events,
/// cancellation, input validation, the invalid-output path, permission enforcement and exporter
/// substitution.
/// </summary>
public class LandXmlExportWorkflowTests
{
    [Fact]
    public async Task Dispatch_Exported_ReturnsPopulatedReportAndWritesFile()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);
        try
        {
            WorkflowResult<LandXmlExportReport> result =
                await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                    workflow, context, CancellationToken.None);

            Assert.True(result.Success);
            LandXmlExportReport report = result.Data!;
            Assert.Equal("Exported", report.Summary.Status);
            Assert.Equal(path, report.Summary.OutputPath);
            Assert.True(report.Summary.FileSizeBytes > 0);
            Assert.Equal(3, report.Summary.ExportedCount);
            Assert.Equal(0, report.Summary.SkippedCount);
            Assert.Equal(6, report.Statistics.TotalCollected);
            Assert.Equal(3, report.ExportedObjects.Count);
            Assert.Contains(report.Recommendations, r => r.Title == "Export completed successfully");
            Assert.Equal("landxml.export", report.Execution.WorkflowName);
            Assert.Equal(6, report.Execution.TotalSteps);
            Assert.Equal(6, report.Execution.CompletedSteps);
            Assert.True(File.Exists(path));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_ExporterSubstitution_ReceivesCollectedData()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);
        try
        {
            await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None);

            Assert.Equal(1, container.Exporter.Calls);
            Assert.NotNull(container.Exporter.LastData);
            Assert.Equal(path, container.Exporter.LastData!.OutputPath);
            Assert.Equal(2, container.Exporter.LastData.AlignmentCount);
            Assert.Equal(3, container.Exporter.LastData.ProfileCount);
            Assert.Equal(1, container.Exporter.LastData.SurfaceCount);
            Assert.True(container.Exporter.LastData.IncludeAlignments);
            Assert.False(container.Exporter.LastData.IncludeCorridors);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_ReportsProgressAcrossAllStages()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        WorkflowContext context = ExportContext(container.Provider, progress: progress);
        try
        {
            await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None);

            string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
            Assert.Contains("Validate Input", stages);
            Assert.Contains("Collect Export Data", stages);
            Assert.Contains("Build Export Options", stages);
            Assert.Contains("Execute Export", stages);
            Assert.Contains("Validate Output", stages);
            Assert.Contains("Generate Report", stages);
            Assert.Contains("Complete", stages);
            Assert.Contains(progress.Reports, r => r.Percent == 100);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_NotSupported_ReturnsStructuredReport()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer(exporter: new FakeLandXmlExporter(
            status: LandXmlExportStatus.NotSupported,
            reason: "Not available in the read-only workflow layer."));
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);

        WorkflowResult<LandXmlExportReport> result =
            await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Not Supported", result.Data!.Summary.Status);
        Assert.Equal("Not available in the read-only workflow layer.", result.Data.Summary.NotSupportedReason);
        Assert.Equal(0, result.Data.Summary.FileSizeBytes);
        Assert.Empty(result.Data.ExportedObjects);
        Assert.Contains(result.Data.Recommendations, r => r.Title == "Export not supported by installed API");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Dispatch_InvalidOutput_ThrowsStepFailed()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer(exporter: new FakeLandXmlExporter(writeFile: false));
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.StepFailed, ex.Code);
    }

    [Fact]
    public async Task Dispatch_EmptyOutputPath_ThrowsInvalidParameters()
    {
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(""));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_NoObjectTypes_ThrowsInvalidParameters()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(
            path, includeAlignments: false, includeProfiles: false, includeSurfaces: false));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_FileExistsWithoutOverwrite_ThrowsInvalidParameters()
    {
        string path = SampleData.TempXmlPath();
        File.WriteAllText(path, "existing");
        try
        {
            Container container = CreateContainer();
            var workflow = new LandXmlExportWorkflow(Request(path));
            var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
            WorkflowContext context = ExportContext(container.Provider);

            WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
                dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                    workflow, context, CancellationToken.None));

            Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_FileExistsWithOverwrite_Succeeds()
    {
        string path = SampleData.TempXmlPath();
        File.WriteAllText(path, "existing");
        try
        {
            Container container = CreateContainer();
            var workflow = new LandXmlExportWorkflow(Request(path, overwriteExisting: true));
            var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
            WorkflowContext context = ExportContext(container.Provider);

            WorkflowResult<LandXmlExportReport> result =
                await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                    workflow, context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Exported", result.Data!.Summary.Status);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Dispatch_RequiresExportPermission_ThrowsPermissionDenied()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider, permission: CommandPermission.ReadOnly);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.PermissionDenied, ex.Code);
    }

    [Fact]
    public async Task Dispatch_PreCancelled_ThrowsBeforeAnyStepRuns()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        WorkflowContext context = ExportContext(container.Provider, cts.Token, progress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, cts.Token));
    }

    [Fact]
    public async Task Dispatch_RaisesDomainEvents()
    {
        string path = SampleData.TempXmlPath();
        Container container = CreateContainer();
        var workflow = new LandXmlExportWorkflow(Request(path));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = ExportContext(container.Provider);
        try
        {
            await dispatcher.DispatchAsync<LandXmlExportWorkflow, LandXmlExportReport>(
                workflow, context, CancellationToken.None);

            Assert.Contains(container.Events.Published, e => e is WorkflowStarted);
            Assert.Contains(container.Events.Published, e => e is WorkflowCompleted);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static LandXmlExportRequest Request(
        string outputPath,
        bool includeAlignments = true,
        bool includeProfiles = true,
        bool includeSurfaces = true,
        bool includeCorridors = false,
        bool includePipeNetworks = false,
        bool overwriteExisting = false)
        => new()
        {
            OutputPath = outputPath,
            IncludeAlignments = includeAlignments,
            IncludeProfiles = includeProfiles,
            IncludeSurfaces = includeSurfaces,
            IncludeCorridors = includeCorridors,
            IncludePipeNetworks = includePipeNetworks,
            OverwriteExisting = overwriteExisting,
        };

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
