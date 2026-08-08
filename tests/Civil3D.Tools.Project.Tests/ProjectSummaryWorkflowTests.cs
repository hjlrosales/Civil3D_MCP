using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Project.Analysis;
using Civil3D.Tools.Project.Dtos;
using Civil3D.Tools.Project.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Project.Tests.ProjectHarness;
using static Civil3D.Tools.Project.Tests.TestDoubles;

namespace Civil3D.Tools.Project.Tests;

/// <summary>
/// The project-summary workflow end to end through the real dispatcher: orchestration over the
/// fake domain services, report composition, progress milestones, events, cancellation and the
/// no-active-drawing failure path.
/// </summary>
public class ProjectSummaryWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new ProjectSummaryWorkflow();

        WorkflowResult<ProjectSummaryReport> result = await container.Provider
            .GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                workflow, ProjectContext(container.Provider));

        Assert.True(result.Success);
        ProjectSummaryReport report = result.Data;

        // Drawing identity flows through.
        Assert.Equal("ProjectSample.dwg", report.Overview.DrawingName);
        Assert.Equal("AC1032", report.Overview.DrawingVersion);
        Assert.Equal("25.0", report.Overview.Civil3DVersion);

        // Inventory reflects the canned sample: 2 alignments, 1 surface, 1 profile, 1 corridor,
        // 1 pipe network, 2 COGO points, 2 styles.
        Assert.Equal(2, report.Inventory.AlignmentCount);
        Assert.Equal(1, report.Inventory.SurfaceCount);
        Assert.Equal(1, report.Inventory.ProfileCount);
        Assert.Equal(1, report.Inventory.CorridorCount);
        Assert.Equal(1, report.Inventory.PipeNetworkCount);
        Assert.Equal(2, report.Inventory.CogoPointCount);
        Assert.Equal(2, report.Inventory.StyleCount);

        // 3,400 entities and 2 xrefs classify the drawing as Medium (10 <= score < 25).
        Assert.Equal(ProjectComplexity.Medium, report.Complexity.Classification);

        // References: 2 xrefs, all style ids resolve (ids 1..2, style set 1..2), no orphans.
        Assert.Equal(2, report.References.TotalXRefs);
        Assert.Equal(0, report.References.MissingStyleCount);
        Assert.Equal(0, report.References.OrphanedObjectCount);

        // The sample deliberately leaves one alignment without a description and one style unused.
        Assert.Contains(report.Recommendations, r => r.Title == "Review unused styles");
        Assert.Contains(report.Recommendations, r => r.Title == "Missing metadata");
        Assert.Contains(report.Recommendations, r => r.Title == "Reference synchronization");

        // Execution summary reflects the five steps.
        Assert.Equal("project.summary.report", report.Execution.WorkflowName);
        Assert.Equal(5, report.Execution.TotalSteps);
        Assert.Equal(5, report.Execution.CompletedSteps);
        Assert.True(report.Execution.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Dispatch_CompleteWorkflow_PublishesWorkflowEvents()
    {
        Container container = CreateContainer();
        var workflow = new ProjectSummaryWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                workflow, ProjectContext(container.Provider));

        Assert.Single(container.Events.Published.OfType<WorkflowStarted>());
        Assert.Single(container.Events.Published.OfType<WorkflowCompleted>());
        Assert.Empty(container.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task Dispatch_ReportsProgressMilestones()
    {
        Container container = CreateContainer();
        var progress = new RecordingProgressReporter();
        var workflow = new ProjectSummaryWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                workflow, ProjectContext(container.Provider, progress: progress));

        Assert.NotEmpty(progress.Reports);
        Assert.Contains(progress.Reports, r => r.Stage == "Validate Input");
        Assert.Contains(progress.Reports, r => r.Stage == "Collect Drawing Information");
        Assert.Contains(progress.Reports, r => r.Stage == "Collect Domain Objects");
        Assert.Contains(progress.Reports, r => r.Stage == "Analyze Relationships");
        Assert.Contains(progress.Reports, r => r.Stage == "Generate Summary");
        Assert.Equal(100, progress.Reports[^1].Percent);
    }

    [Fact]
    public async Task Dispatch_CancelledBeforeStart_ThrowsCancelled()
    {
        Container container = CreateContainer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var workflow = new ProjectSummaryWorkflow();

        // A token cancelled before dispatch fires before the dispatcher wraps it (the workflow
        // start event publish honours the token), matching the framework's established behaviour.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                    workflow, ProjectContext(container.Provider, cancellationToken: cts.Token), cts.Token));
    }

    [Fact]
    public async Task Dispatch_NoActiveDrawing_FailsWithInvalidParameters()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var workflow = new ProjectSummaryWorkflow();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                    workflow, ProjectContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_CollectsStatisticsExactlyOnce()
    {
        var statistics = new FakeDrawingStatisticsService(SampleData.Statistics());
        Container container = CreateContainer(statistics: statistics);
        var workflow = new ProjectSummaryWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                workflow, ProjectContext(container.Provider));

        Assert.Equal(1, statistics.Calls);
    }

    [Fact]
    public async Task Dispatch_NegativeThresholds_FailsWithInvalidParameters()
    {
        Container container = CreateContainer();
        var options = new ProjectSummaryOptions { LargeDrawingEntityThreshold = -1 };
        var workflow = new ProjectSummaryWorkflow(options);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<ProjectSummaryWorkflow, ProjectSummaryReport>(
                    workflow, ProjectContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
        Assert.Empty(container.Events.Published.OfType<WorkflowCompleted>());
    }
}
