using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Corridor.Dtos;
using Civil3D.Tools.Corridor.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Civil3D.Tools.Corridor.Tests.CorridorHarness;
using static Civil3D.Tools.Corridor.Tests.TestDoubles;

namespace Civil3D.Tools.Corridor.Tests;

/// <summary>
/// The corridor-analysis workflow end to end through the real dispatcher: orchestration over the
/// fake corridor service, report composition, single-versus-all corridor scoping, progress
/// milestones, events, cancellation, validation failures and the missing-corridor error path.
/// </summary>
public class CorridorWorkflowTests
{
    [Fact]
    public async Task Dispatch_AllCorridors_ReturnsPopulatedReport()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request());
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        WorkflowResult<CorridorAnalysisReport> result =
            await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        CorridorAnalysisReport report = result.Data!;
        Assert.Equal("Attention Required", report.Verdict);
        Assert.Equal(3, report.Corridors.Count);
        Assert.Equal(7, report.Issues.Count);
        Assert.NotNull(report.Statistics);
        Assert.Equal(3, report.Statistics!.CorridorCount);
        Assert.Equal(4, report.Recommendations.Count);
        Assert.Equal("corridor.analysis.report", report.Execution.WorkflowName);
        Assert.Equal(5, report.Execution.TotalSteps);
        Assert.Equal(5, report.Execution.CompletedSteps);
    }

    [Fact]
    public async Task Dispatch_SingleCorridor_OnlyThatCorridorAnalyzed()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request(corridorId: 1));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        WorkflowResult<CorridorAnalysisReport> result =
            await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        CorridorAnalysisReport report = result.Data!;
        CorridorSummary summary = Assert.Single(report.Corridors);
        Assert.Equal("Mainline", summary.Name);
        Assert.Equal("Healthy", report.Verdict);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressAcrossAllStages()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request());
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        WorkflowContext context = CorridorContext(container.Provider, progress: progress);

        await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
            workflow, context, CancellationToken.None);

        string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
        Assert.Contains("Validate Input", stages);
        Assert.Contains("Load Corridor Data", stages);
        Assert.Contains("Analyze Corridors", stages);
        Assert.Contains("Generate Recommendations", stages);
        Assert.Contains("Generate Report", stages);
        Assert.Contains("Complete", stages);
        Assert.Contains(progress.Reports, r => r.Percent == 100);
    }

    [Fact]
    public async Task Dispatch_MissingCorridor_ThrowsDomainException()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request(corridorId: 999));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task Dispatch_InvalidId_ThrowsInvalidParameters()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request(corridorId: 0));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None));

        Assert.Equal(WorkflowErrorCode.InvalidParameters, ex.Code);
    }

    [Fact]
    public async Task Dispatch_PreCancelled_ThrowsBeforeAnyStepRuns()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request());
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        RecordingProgressReporter progress = new();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        WorkflowContext context = CorridorContext(container.Provider, cts.Token, progress);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, cts.Token));
    }

    [Fact]
    public async Task Dispatch_RaisesDomainEvents()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request());
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
            workflow, context, CancellationToken.None);

        Assert.Contains(container.Events.Published, e => e is WorkflowStarted);
        Assert.Contains(container.Events.Published, e => e is WorkflowCompleted);
    }

    [Fact]
    public async Task Dispatch_RecommendationsDisabled_EmptyRecommendations()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request(includeRecommendations: false));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        WorkflowResult<CorridorAnalysisReport> result =
            await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Recommendations);
        Assert.Equal("Attention Required", result.Data.Verdict);
    }

    [Fact]
    public async Task Dispatch_StatisticsDisabled_NullStatistics()
    {
        Container container = CreateContainer();
        var workflow = new CorridorAnalysisWorkflow(Request(includeStatistics: false));
        var dispatcher = container.Provider.GetRequiredService<IWorkflowDispatcher>();
        WorkflowContext context = CorridorContext(container.Provider);

        WorkflowResult<CorridorAnalysisReport> result =
            await dispatcher.DispatchAsync<CorridorAnalysisWorkflow, CorridorAnalysisReport>(
                workflow, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Statistics);
        Assert.NotEmpty(result.Data.Issues);
    }

    private static CorridorAnalysisRequest Request(
        long? corridorId = null, bool includeStatistics = true, bool includeRecommendations = true)
        => new()
        {
            CorridorId = corridorId,
            IncludeStatistics = includeStatistics,
            IncludeRecommendations = includeRecommendations,
        };
}
