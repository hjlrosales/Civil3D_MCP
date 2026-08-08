using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Civil3D.Tools.Health.Analysis;
using Civil3D.Tools.Health.Dtos;
using Civil3D.Tools.Health.Workflow;
using Xunit;
using static Civil3D.Tools.Health.Tests.HealthHarness;
using static Civil3D.Tools.Health.Tests.TestDoubles;

namespace Civil3D.Tools.Health.Tests;

/// <summary>
/// The drawing-health workflow end to end through the real dispatcher: orchestration over the
/// fake domain services, report composition, progress milestones, events, cancellation and the
/// no-active-drawing failure path.
/// </summary>
public class DrawingHealthWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new DrawingHealthWorkflow();

        WorkflowResult<DrawingHealthReport> result = await container.Provider
            .GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                workflow, HealthContext(container.Provider));

        Assert.True(result.Success);
        DrawingHealthReport report = result.Data;

        // Drawing identity flows through.
        Assert.Equal("HealthSample.dwg", report.DrawingName);
        Assert.Equal("AC1032", report.DrawingVersion);
        Assert.True(report.IsModified);
        Assert.Equal(2, report.Statistics.XRefCount);

        // Findings reflect the canned sample: one undocumented alignment, one undocumented + locked
        // COGO point, and unsaved changes.
        Assert.Contains(report.Issues, i => i.Code == "MISSING_ALIGNMENT_DESCRIPTION" && i.RelatedObject == "Side Road");
        Assert.Contains(report.Issues, i => i.Code == "LOCKED_COGO_POINTS");
        Assert.Contains(report.Issues, i => i.Code == "MISSING_COGO_POINT_DESCRIPTION");
        Assert.Contains(report.Issues, i => i.Code == "UNSAVED_CHANGES");

        // Statistics roll up over the inspected population (2 alignments + 1 surface + 1 profile
        // + 1 corridor + 1 network + 2 points + 1 style = 9 objects).
        Assert.Equal(9, report.Health.ObjectCount);
        Assert.Equal(report.Issues.Count, report.Health.TotalIssues);

        // Execution summary reflects the five steps.
        Assert.Equal("drawing.health.report", report.Execution.WorkflowName);
        Assert.Equal(5, report.Execution.TotalSteps);
        Assert.Equal(5, report.Execution.CompletedSteps);
        Assert.True(report.Execution.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Dispatch_CompleteWorkflow_PublishesWorkflowEvents()
    {
        Container container = CreateContainer();
        var workflow = new DrawingHealthWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                workflow, HealthContext(container.Provider));

        Assert.Single(container.Events.Published.OfType<WorkflowStarted>());
        Assert.Single(container.Events.Published.OfType<WorkflowCompleted>());
        Assert.Empty(container.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task Dispatch_ReportsProgressMilestones()
    {
        Container container = CreateContainer();
        var progress = new RecordingProgressReporter();
        var workflow = new DrawingHealthWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                workflow, HealthContext(container.Provider, progress: progress));

        Assert.NotEmpty(progress.Reports);
        Assert.Contains(progress.Reports, r => r.Stage == "Validate Input");
        Assert.Contains(progress.Reports, r => r.Stage == "Collect Drawing Information");
        Assert.Contains(progress.Reports, r => r.Stage == "Collect Domain Data");
        Assert.Contains(progress.Reports, r => r.Stage == "Analyze Results");
        Assert.Contains(progress.Reports, r => r.Stage == "Generate Report");
        Assert.Equal(100, progress.Reports[^1].Percent);
    }

    [Fact]
    public async Task Dispatch_CancelledBeforeStart_ThrowsCancelled()
    {
        Container container = CreateContainer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var workflow = new DrawingHealthWorkflow();

        // A token cancelled before dispatch fires before the dispatcher wraps it (the workflow
        // start event publish honours the token), matching the framework's established behaviour.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                    workflow, HealthContext(container.Provider, cancellationToken: cts.Token), cts.Token));
    }

    [Fact]
    public async Task Dispatch_NoActiveDrawing_FailsWithInvalidParameters()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var workflow = new DrawingHealthWorkflow();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                    workflow, HealthContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_CollectsStatisticsExactlyOnce()
    {
        var statistics = new FakeDrawingStatisticsService(SampleData.Statistics());
        Container container = CreateContainer(statistics: statistics);
        var workflow = new DrawingHealthWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                workflow, HealthContext(container.Provider));

        Assert.Equal(1, statistics.Calls);
    }
    [Fact]
    public async Task Dispatch_NegativeThresholds_FailsWithInvalidParameters()
    {
        Container container = CreateContainer();
        var options = new HealthAnalyzerOptions { LargeDrawingEntityThreshold = -1 };
        var workflow = new DrawingHealthWorkflow(options);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DrawingHealthWorkflow, DrawingHealthReport>(
                    workflow, HealthContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
        Assert.Empty(container.Events.Published.OfType<WorkflowCompleted>());
    }
}
