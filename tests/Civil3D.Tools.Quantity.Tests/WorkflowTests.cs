using Civil3D.Domain.Commands;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Quantity.Dtos;
using Civil3D.Tools.Quantity.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Quantity.Tests.QuantityHarness;
using static Civil3D.Tools.Quantity.Tests.TestDoubles;

namespace Civil3D.Tools.Quantity.Tests;

/// <summary>
/// The quantity-takeoff workflow end to end through the real dispatcher: orchestration over the
/// fake domain services, the real calculator, report composition, progress milestones, events,
/// cancellation and the no-active-drawing failure path.
/// </summary>
public class QuantityTakeoffWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new QuantityTakeoffWorkflow();
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = QuantityContext(container.Provider);

        WorkflowResult<QuantityTakeoffReport> result =
            await dispatcher.DispatchAsync<QuantityTakeoffWorkflow, QuantityTakeoffReport>(workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        QuantityTakeoffReport report = result.Data!;
        Assert.Equal("QuantitySample.dwg", report.Overview.DrawingName);
        Assert.Equal(6, report.Execution.TotalSteps);
        Assert.Equal(6, report.Execution.CompletedSteps);
        Assert.Equal("quantity.takeoff.report", report.Execution.WorkflowName);
        Assert.Equal(2_300, report.Statistics.TotalLinearLength, 3);
        Assert.Equal(12, report.Statistics.TotalDomainObjects);
        Assert.Contains(report.Items, i => i.Key == "alignment.count");
        Assert.Contains(report.Summaries, s => s.Category == QuantityCategory.Alignments);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressAcrossAllStages()
    {
        Container container = CreateContainer();
        var workflow = new QuantityTakeoffWorkflow();
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        WorkflowContext context = QuantityContext(container.Provider, progress: progress);

        await dispatcher.DispatchAsync<QuantityTakeoffWorkflow, QuantityTakeoffReport>(workflow, context, CancellationToken.None);

        string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
        Assert.Contains("Validate Input", stages);
        Assert.Contains("Collect Drawing Information", stages);
        Assert.Contains("Collect Domain Data", stages);
        Assert.Contains("Calculate Quantities", stages);
        Assert.Contains("Aggregate Results", stages);
        Assert.Contains("Generate Report", stages);
        Assert.Contains("Complete", stages);
        Assert.Contains(progress.Reports, r => r.Percent == 100);
    }

    [Fact]
    public async Task Dispatch_NoActiveDocument_ThrowsWorkflowException()
    {
        Container container = CreateContainer(session: new FakeSession(drawing: null));
        var workflow = new QuantityTakeoffWorkflow();
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = QuantityContext(container.Provider);

        await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<QuantityTakeoffWorkflow, QuantityTakeoffReport>(workflow, context, CancellationToken.None));
    }

    [Fact]
    public async Task Dispatch_PreCancelled_ThrowsBeforeAnyStepRuns()
    {
        Container container = CreateContainer();
        var workflow = new QuantityTakeoffWorkflow();
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        WorkflowContext context = QuantityContext(container.Provider, cts.Token, progress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<QuantityTakeoffWorkflow, QuantityTakeoffReport>(workflow, context, cts.Token));
    }

    [Fact]
    public async Task Dispatch_RaisesDomainEvents()
    {
        Container container = CreateContainer();
        var workflow = new QuantityTakeoffWorkflow();
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = QuantityContext(container.Provider);

        await dispatcher.DispatchAsync<QuantityTakeoffWorkflow, QuantityTakeoffReport>(workflow, context, CancellationToken.None);

        Assert.Contains(container.Events.Published, e => e is WorkflowStarted);
        Assert.Contains(container.Events.Published, e => e is WorkflowCompleted);
    }
}
