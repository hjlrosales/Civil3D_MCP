using Civil3D.Domain.Errors;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.CutFill.Dtos;
using Civil3D.Tools.CutFill.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.CutFill.Tests.CutFillHarness;
using static Civil3D.Tools.CutFill.Tests.TestDoubles;

namespace Civil3D.Tools.CutFill.Tests;

/// <summary>
/// The cut/fill workflow end to end through the real dispatcher: orchestration over the fake
/// surface service and fake calculator, report composition, progress milestones, events,
/// cancellation, validation failures, the missing-surface error path and calculator
/// substitution.
/// </summary>
public class CutFillWorkflowTests
{
    [Fact]
    public async Task Dispatch_CompleteWorkflow_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        WorkflowResult<CutFillReport> result =
            await dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        CutFillReport report = result.Data!;
        Assert.Equal("EG", report.Summary.ExistingSurfaceName);
        Assert.Equal("FG", report.Summary.ProposedSurfaceName);
        Assert.Equal("Predominantly Cut", report.Summary.Verdict);
        Assert.Equal(12_000, report.Summary.CutVolume);
        Assert.Equal(4_000, report.Summary.FillVolume);
        Assert.Equal(8_000, report.Summary.NetVolume);
        Assert.Equal(4, report.Differences.Count);
        Assert.NotNull(report.Statistics);
        Assert.Equal(3, report.Recommendations.Count);
        Assert.Equal("calculate.cut.fill", report.Execution.WorkflowName);
        Assert.Equal(6, report.Execution.TotalSteps);
        Assert.Equal(6, report.Execution.CompletedSteps);
    }

    [Fact]
    public async Task Dispatch_CalculatorSubstitution_ReceivesLoadedSurfaces()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        await dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
            workflow, context, CancellationToken.None);

        Assert.Equal(1, container.Calculator.Calls);
        Assert.NotNull(container.Calculator.LastData);
        Assert.Equal(1, container.Calculator.LastData!.ExistingSurface.Id);
        Assert.Equal(2, container.Calculator.LastData.ProposedSurface.Id);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressAcrossAllStages()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        WorkflowContext context = CutFillContext(container.Provider, progress: progress);

        await dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
            workflow, context, CancellationToken.None);

        string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
        Assert.Contains("Validate Input", stages);
        Assert.Contains("Load Surfaces", stages);
        Assert.Contains("Prepare Calculation", stages);
        Assert.Contains("Execute Calculation", stages);
        Assert.Contains("Analyze Results", stages);
        Assert.Contains("Generate Report", stages);
        Assert.Contains("Complete", stages);
        Assert.Contains(progress.Reports, r => r.Percent == 100);
    }

    [Fact]
    public async Task Dispatch_MissingSurface_ThrowsDomainException()
    {
        Container container = CreateContainer(surfaces: new FakeSurfaceService(SampleData.Contrasting()));
        var workflow = new CutFillWorkflow(Request(1, 999));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task Dispatch_IdenticalIds_ThrowsInvalidParameters()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 1));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_NotSupportedCalculator_ProducesNotSupportedReport()
    {
        Container container = CreateContainer(calculator: new FakeCutFillCalculator(
            new Civil3D.Tools.CutFill.Abstractions.CutFillCalculationResult
            {
                Status = Civil3D.Tools.CutFill.Abstractions.CutFillStatus.NotSupported,
                NotSupportedReason = "Read-only volumes are unavailable.",
            }));
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        WorkflowResult<CutFillReport> result =
            await dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Not Supported", result.Data!.Summary.Verdict);
        Assert.Equal("Read-only volumes are unavailable.", result.Data.Summary.NotSupportedReason);
        Assert.Null(result.Data.Statistics);
        Assert.Empty(result.Data.Recommendations);
    }

    [Fact]
    public async Task Dispatch_PreCancelled_ThrowsBeforeAnyStepRuns()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        WorkflowContext context = CutFillContext(container.Provider, cts.Token, progress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
                workflow, context, cts.Token));
    }

    [Fact]
    public async Task Dispatch_RaisesDomainEvents()
    {
        Container container = CreateContainer();
        var workflow = new CutFillWorkflow(Request(1, 2));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CutFillContext(container.Provider);

        await dispatcher.DispatchAsync<CutFillWorkflow, CutFillReport>(
            workflow, context, CancellationToken.None);

        Assert.Contains(container.Events.Published, e => e is WorkflowStarted);
        Assert.Contains(container.Events.Published, e => e is WorkflowCompleted);
    }

    private static CutFillRequest Request(long existingId, long proposedId) => new()
    {
        ExistingSurfaceId = existingId,
        ProposedSurfaceId = proposedId,
    };
}
