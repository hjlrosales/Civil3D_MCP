using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Surface.Dtos;
using Civil3D.Tools.Surface.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Surface.Tests.SurfaceHarness;
using static Civil3D.Tools.Surface.Tests.TestDoubles;

namespace Civil3D.Tools.Surface.Tests;

/// <summary>
/// The surface-comparison workflow end to end through the real dispatcher: orchestration over the
/// fake surface service, the pure comparer, report composition, progress milestones, events,
/// cancellation, validation failures and the missing-surface error path.
/// </summary>
public class SurfaceComparisonWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new SurfaceComparisonWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = SurfaceContext(container.Provider);

        WorkflowResult<SurfaceComparisonReport> result =
            await dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        SurfaceComparisonReport report = result.Data!;
        Assert.Equal("EG", report.Summary.ExistingSurfaceName);
        Assert.Equal("FG", report.Summary.ProposedSurfaceName);
        Assert.Equal("Review Required", report.Summary.Verdict);
        Assert.Equal(6, report.Metrics.Count);
        Assert.Equal(5, report.Differences.Count);
        Assert.NotNull(report.Statistics);
        Assert.Equal(3, report.Recommendations.Count);
        Assert.Equal("surface.comparison.report", report.Execution.WorkflowName);
        Assert.Equal(5, report.Execution.TotalSteps);
        Assert.Equal(5, report.Execution.CompletedSteps);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressAcrossAllStages()
    {
        Container container = CreateContainer();
        var workflow = new SurfaceComparisonWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        WorkflowContext context = SurfaceContext(container.Provider, progress: progress);

        await dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
            workflow, context, CancellationToken.None);

        string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
        Assert.Contains("Validate Input", stages);
        Assert.Contains("Load Surface Metadata", stages);
        Assert.Contains("Load Comparison Data", stages);
        Assert.Contains("Analyze Differences", stages);
        Assert.Contains("Generate Report", stages);
        Assert.Contains("Complete", stages);
        Assert.Contains(progress.Reports, r => r.Percent == 100);
    }

    [Fact]
    public async Task Dispatch_MissingSurface_ThrowsDomainException()
    {
        Container container = CreateContainer(surfaces: new FakeSurfaceService(SampleData.Contrasting()));
        var workflow = new SurfaceComparisonWorkflow(Request(1, 999));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = SurfaceContext(container.Provider);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task Dispatch_IdenticalIds_ThrowsInvalidParameters()
    {
        Container container = CreateContainer();
        var workflow = new SurfaceComparisonWorkflow(Request(1, 1));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = SurfaceContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_PreCancelled_ThrowsBeforeAnyStepRuns()
    {
        Container container = CreateContainer();
        var workflow = new SurfaceComparisonWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        WorkflowContext context = SurfaceContext(container.Provider, cts.Token, progress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
                workflow, context, cts.Token));
    }

    [Fact]
    public async Task Dispatch_RaisesDomainEvents()
    {
        Container container = CreateContainer();
        var workflow = new SurfaceComparisonWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = SurfaceContext(container.Provider);

        await dispatcher.DispatchAsync<SurfaceComparisonWorkflow, SurfaceComparisonReport>(
            workflow, context, CancellationToken.None);

        Assert.Contains(container.Events.Published, e => e is WorkflowStarted);
        Assert.Contains(container.Events.Published, e => e is WorkflowCompleted);
    }

    private static SurfaceComparisonRequest Request(long existingId, long proposedId) => new()
    {
        ExistingSurfaceId = existingId,
        ProposedSurfaceId = proposedId,
    };
}
