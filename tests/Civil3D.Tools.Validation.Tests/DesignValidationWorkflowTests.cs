using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Validation.Tests.ValidationHarness;
using static Civil3D.Tools.Validation.Tests.TestDoubles;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// The design-validation workflow end to end through the real dispatcher: orchestration over the
/// fake domain services, the real rule engine, report composition, progress milestones, events,
/// cancellation, the no-active-drawing failure path and the no-rules failure path.
/// </summary>
public class DesignValidationWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new DesignValidationWorkflow();

        WorkflowResult<DesignValidationReport> result = await container.Provider
            .GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                workflow, ValidationContext(container.Provider));

        Assert.True(result.Success);
        DesignValidationReport report = result.Data;

        Assert.Equal("ValidationSample.dwg", report.DrawingName);
        Assert.Equal("AC1032", report.DrawingVersion);

        // The canned sample triggers findings from every rule.
        Assert.Contains(report.Issues, i => i.Code == "DUPLICATE_ALIGNMENT_NAME");
        Assert.Contains(report.Issues, i => i.Code == "UNRESOLVED_ALIGNMENT_REFERENCE");
        Assert.Contains(report.Issues, i => i.Code == "UNUSED_STYLE");
        Assert.Contains(report.Issues, i => i.Code == "DUPLICATE_COGO_POINT_NUMBER");
        Assert.Contains(report.Issues, i => i.Code == "PIPE_NETWORK_WITHOUT_STRUCTURES");

        // The engine ran all eight rules and found errors, warnings and information.
        Assert.Equal(8, report.Summary.RulesRegistered);
        Assert.Equal(8, report.Summary.RulesExecuted);
        Assert.Equal(0, report.Summary.RuleFailures);
        Assert.True(report.Summary.ErrorCount > 0);
        Assert.Equal(report.Issues.Count, report.Summary.TotalIssues);

        // Execution summary reflects the five workflow steps.
        Assert.Equal("design.validation.report", report.Execution.WorkflowName);
        Assert.Equal(5, report.Execution.TotalSteps);
        Assert.Equal(5, report.Execution.CompletedSteps);
        Assert.True(report.Execution.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Dispatch_CompleteWorkflow_PublishesWorkflowEvents()
    {
        Container container = CreateContainer();
        var workflow = new DesignValidationWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                workflow, ValidationContext(container.Provider));

        Assert.Single(container.Events.Published.OfType<WorkflowStarted>());
        Assert.Single(container.Events.Published.OfType<WorkflowCompleted>());
        Assert.Empty(container.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task Dispatch_ReportsProgressMilestones()
    {
        Container container = CreateContainer();
        var progress = new RecordingProgressReporter();
        var workflow = new DesignValidationWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                workflow, ValidationContext(container.Provider, progress: progress));

        Assert.NotEmpty(progress.Reports);
        Assert.Contains(progress.Reports, r => r.Stage == "Validate Input");
        Assert.Contains(progress.Reports, r => r.Stage == "Collect Domain Data");
        Assert.Contains(progress.Reports, r => r.Stage == "Execute Validation Rules");
        Assert.Contains(progress.Reports, r => r.Stage == "Aggregate Results");
        Assert.Contains(progress.Reports, r => r.Stage == "Generate Report");
        Assert.Contains(progress.Reports, r => r.Stage == "Complete");
        Assert.Equal(100, progress.Reports[^1].Percent);
    }

    [Fact]
    public async Task Dispatch_CancelledBeforeStart_ThrowsCancelled()
    {
        Container container = CreateContainer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var workflow = new DesignValidationWorkflow();

        // A token cancelled before dispatch fires before the dispatcher wraps it (the workflow
        // start event publish honours the token), matching the framework's established behaviour.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                    workflow, ValidationContext(container.Provider, cancellationToken: cts.Token), cts.Token));
    }

    [Fact]
    public async Task Dispatch_NoActiveDrawing_FailsWithInvalidParameters()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var workflow = new DesignValidationWorkflow();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                    workflow, ValidationContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_NoRulesRegistered_FailsWithInvalidParameters()
    {
        Container container = CreateContainer(rules: Array.Empty<IValidationRule>());
        var workflow = new DesignValidationWorkflow();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            container.Provider.GetRequiredService<IWorkflowDispatcher>()
                .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                    workflow, ValidationContext(container.Provider)));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
        Assert.Empty(container.Events.Published.OfType<WorkflowCompleted>());
    }

    [Fact]
    public async Task Dispatch_CollectsStatisticsExactlyOnce()
    {
        var statistics = new FakeDrawingStatisticsService(SampleData.Statistics());
        Container container = CreateContainer(statistics: statistics);
        var workflow = new DesignValidationWorkflow();

        await container.Provider.GetRequiredService<IWorkflowDispatcher>()
            .DispatchAsync<DesignValidationWorkflow, DesignValidationReport>(
                workflow, ValidationContext(container.Provider));

        Assert.Equal(1, statistics.Calls);
    }
}
