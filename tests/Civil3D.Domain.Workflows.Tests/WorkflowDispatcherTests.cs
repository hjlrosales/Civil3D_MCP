using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Domain.Workflows.Tests.TestWorkflows;

namespace Civil3D.Domain.Workflows.Tests;

/// <summary>
/// The full dispatcher pipeline: validation aggregation, permission check, step execution order
/// and early stop, progress milestones, cancellation and timeout, step-failure and domain-failure
/// mapping, event ordering, missing-handler handling and result serialization.
/// </summary>
public class WorkflowDispatcherTests
{
    private sealed record Harness(
        IWorkflowDispatcher Dispatcher,
        InMemoryDomainEventDispatcher Events,
        FakeRecorder Recorder,
        ServiceProvider Provider);

    private static Harness Create(
        Microsoft.Extensions.Logging.ILogger<WorkflowDispatcher>? logger = null,
        params IWorkflowValidator<SampleWorkflow>[] validators)
    {
        var services = new ServiceCollection();
        var events = new InMemoryDomainEventDispatcher();
        var recorder = new FakeRecorder();

        services.AddSingleton<IDomainEventDispatcher>(events);
        services.AddSingleton(recorder);
        services.AddSingleton<IWorkflowDispatcher>(sp => new WorkflowDispatcher(
            sp,
            sp.GetRequiredService<IDomainEventDispatcher>(),
            logger ?? NullLogger<WorkflowDispatcher>.Instance));

        services.AddTransient<IWorkflowHandler<SampleWorkflow, SampleResult>, GenericHandler<SampleWorkflow>>();
        services.AddTransient<IWorkflowHandler<StopAfterStepWorkflow, SampleResult>, GenericHandler<StopAfterStepWorkflow>>();
        services.AddTransient<IWorkflowHandler<ModifyPermissionWorkflow, SampleResult>, GenericHandler<ModifyPermissionWorkflow>>();
        services.AddTransient<IWorkflowHandler<FailingStepWorkflow, SampleResult>, GenericHandler<FailingStepWorkflow>>();
        services.AddTransient<IWorkflowHandler<DomainFailStepWorkflow, SampleResult>, GenericHandler<DomainFailStepWorkflow>>();
        services.AddTransient<IWorkflowHandler<TimeoutWorkflow, SampleResult>, GenericHandler<TimeoutWorkflow>>();
        services.AddTransient<IWorkflowHandler<CancellableWorkflow, SampleResult>, GenericHandler<CancellableWorkflow>>();
        services.AddTransient<IWorkflowHandler<ZeroTimeoutWorkflow, SampleResult>, GenericHandler<ZeroTimeoutWorkflow>>();

        foreach (IWorkflowValidator<SampleWorkflow> validator in validators)
        {
            services.AddTransient(_ => validator);
        }

        ServiceProvider provider = services.BuildServiceProvider();
        return new Harness(
            provider.GetRequiredService<IWorkflowDispatcher>(),
            events,
            recorder,
            provider);
    }

    private static WorkflowContext Context(
        ServiceProvider provider,
        RecordingProgressReporter? progress = null,
        CommandPermission permission = CommandPermission.ReadOnly,
        CancellationToken cancellationToken = default)
        => new(
            WorkflowName: "test",
            CorrelationId: "c-1",
            SessionId: "s-1",
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(progress ?? new RecordingProgressReporter()),
            Logger: NullLogger.Instance,
            Services: provider,
            Configuration: new Dictionary<string, string>(),
            EffectivePermission: permission,
            StartedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Dispatch_Success_RunsAllStepsAndReturnsResult()
    {
        Harness harness = Create();

        WorkflowResult<SampleResult> result = await harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
            new SampleWorkflow { Value = "ok" },
            Context(harness.Provider));

        Assert.True(result.Success);
        Assert.Equal("done", result.Data.Value);
        Assert.Equal(3, result.Data.StepCount);
        Assert.Equal(["a", "b", "c"], harness.Recorder.RanSteps);
        Assert.True(result.Elapsed >= TimeSpan.Zero);
        Assert.Equal([typeof(WorkflowStarted), typeof(WorkflowCompleted)],
            harness.Events.Published.Select(e => e.GetType()).ToArray());
    }

    [Fact]
    public async Task Dispatch_ValidationFailure_BlocksExecution()
    {
        Harness harness = Create(validators: new ValueRequiredValidator());

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
                new SampleWorkflow { Value = null },
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.ValidationFailed, ex.Code);
        Assert.Empty(harness.Recorder.RanSteps);
        Assert.Single(harness.Events.Published.OfType<WorkflowFailed>());
        Assert.Empty(harness.Events.Published.OfType<WorkflowCompleted>());
    }

    [Fact]
    public async Task Dispatch_MultipleValidators_AggregateFailures()
    {
        Harness harness = Create(validators: [new ValueRequiredValidator(), new ValueMaxLengthValidator()]);

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
                new SampleWorkflow { Value = null },
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.ValidationFailed, ex.Code);
        Assert.Contains("must not be empty", ex.Message);
        Assert.Contains("at most 10", ex.Message);
    }

    [Fact]
    public async Task Dispatch_PermissionDenied_BlocksExecution()
    {
        Harness harness = Create();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<ModifyPermissionWorkflow, SampleResult>(
                new ModifyPermissionWorkflow(),
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.PermissionDenied, ex.Code);
        Assert.Empty(harness.Recorder.RanSteps);
    }

    [Fact]
    public async Task Dispatch_StepFailure_MapsToStepFailed()
    {
        Harness harness = Create();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<FailingStepWorkflow, SampleResult>(
                new FailingStepWorkflow(),
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.StepFailed, ex.Code);
        Assert.Contains("failing", ex.Message);
        WorkflowFailed failed = Assert.Single(harness.Events.Published.OfType<WorkflowFailed>());
        Assert.Equal("StepFailed", failed.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_DomainException_PassesThrough()
    {
        Harness harness = Create();

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Dispatcher.DispatchAsync<DomainFailStepWorkflow, SampleResult>(
                new DomainFailStepWorkflow(),
                Context(harness.Provider)));

        Assert.Equal(DomainErrorCode.EntityNotFound, ex.Code);
        WorkflowFailed failed = Assert.Single(harness.Events.Published.OfType<WorkflowFailed>());
        Assert.Equal("EntityNotFound", failed.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_StepStop_StopsEarly_StillSucceeds()
    {
        Harness harness = Create();

        WorkflowResult<SampleResult> result = await harness.Dispatcher.DispatchAsync<StopAfterStepWorkflow, SampleResult>(
            new StopAfterStepWorkflow(),
            Context(harness.Provider));

        Assert.True(result.Success);
        Assert.Equal(["a", "b"], harness.Recorder.RanSteps);
        Assert.Empty(harness.Events.Published.OfType<WorkflowFailed>());
    }

    [Fact]
    public async Task Dispatch_PreCancelled_MapsToCancelled()
    {
        Harness harness = Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
                new SampleWorkflow { Value = "ok" },
                Context(harness.Provider, cancellationToken: cts.Token),
                cts.Token));
    }

    [Fact]
    public async Task Dispatch_CancelledDuringExecution_MapsToCancelled()
    {
        Harness harness = Create();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(60));

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<CancellableWorkflow, SampleResult>(
                new CancellableWorkflow(),
                Context(harness.Provider, cancellationToken: cts.Token),
                cts.Token));

        Assert.Equal(WorkflowErrorCode.Cancelled, ex.Code);
        WorkflowFailed failed = Assert.Single(harness.Events.Published.OfType<WorkflowFailed>());
        Assert.Equal("Cancelled", failed.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_Timeout_MapsToTimeout()
    {
        Harness harness = Create();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<TimeoutWorkflow, SampleResult>(
                new TimeoutWorkflow(),
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.Timeout, ex.Code);
        WorkflowFailed failed = Assert.Single(harness.Events.Published.OfType<WorkflowFailed>());
        Assert.Equal("Timeout", failed.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_ReportsProgressMilestones()
    {
        Harness harness = Create();
        var progress = new RecordingProgressReporter();

        await harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
            new SampleWorkflow { Value = "ok" },
            Context(harness.Provider, progress: progress));

        string[] stages = progress.Reports.Select(r => r.Stage ?? string.Empty).ToArray();
        Assert.Contains("Validated", stages);
        Assert.Contains("Checked", stages);
        Assert.Contains("a", stages);
        Assert.Contains("Steps complete", stages);
        Assert.Contains("Complete", stages);
        Assert.Equal(100, progress.Reports[^1].Percent);
    }

    [Fact]
    public async Task Dispatch_NoHandler_IsInternal()
    {
        Harness harness = Create();

        WorkflowException ex = await Assert.ThrowsAsync<WorkflowException>(() =>
            harness.Dispatcher.DispatchAsync<UnregisteredWorkflow, SampleResult>(
                new UnregisteredWorkflow(),
                Context(harness.Provider)));

        Assert.Equal(WorkflowErrorCode.Internal, ex.Code);
        Assert.Contains("No handler", ex.Message);
    }

    [Fact]
    public void WorkflowResult_Serialization_RoundTrips()
    {
        var original = new WorkflowResult<SampleResult>(
            new SampleResult("done", 3),
            Success: true,
            ErrorCode: null,
            Message: null,
            StartedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
            FinishedAtUtc: DateTimeOffset.UtcNow);

        string json = JsonSerializer.Serialize(original, SharedJson.Options);
        WorkflowResult<SampleResult>? round = JsonSerializer.Deserialize<WorkflowResult<SampleResult>>(json, SharedJson.Options);

        Assert.NotNull(round);
        Assert.Equal(original.Success, round.Success);
        Assert.Equal(original.Data, round.Data);
        Assert.Equal(original.Elapsed, round.Elapsed);
    }

    [Fact]
    public void WorkflowProgress_TracksPercentAndEstimatesRemaining()
    {
        var progress = new WorkflowProgress(new RecordingProgressReporter());

        Assert.Equal(0, progress.PercentComplete);
        Assert.Null(progress.EstimatedRemaining);

        progress.Report(50, "mid", "half way");

        Assert.Equal(50, progress.PercentComplete);
        Assert.Equal("mid", progress.CurrentStep);
        Assert.Equal("half way", progress.CurrentMessage);
        Assert.True(progress.Elapsed >= TimeSpan.Zero);
        Assert.NotNull(progress.EstimatedRemaining);
    }

    [Fact]
    public void WorkflowProgress_ClampsPercentToRange()
    {
        var progress = new WorkflowProgress(new RecordingProgressReporter());

        progress.Report(250, "over");
        Assert.Equal(100, progress.PercentComplete);

        progress.Report(-5, "under");
        Assert.Equal(0, progress.PercentComplete);
    }

    [Fact]
    public async Task Dispatch_LogsCompletionWithNameAndCorrelation()
    {
        var logger = new RecordingLogger<WorkflowDispatcher>();
        Harness harness = Create(logger: logger);

        await harness.Dispatcher.DispatchAsync<SampleWorkflow, SampleResult>(
            new SampleWorkflow { Value = "ok" },
            Context(harness.Provider));

        string joined = string.Join("\n", logger.Messages);
        Assert.Contains("sample.workflow", joined);
        Assert.Contains("c-1", joined);
        Assert.Contains("completed", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_NonPositiveTimeout_FallsBackToDefault()
    {
        Harness harness = Create();

        WorkflowResult<SampleResult> result = await harness.Dispatcher.DispatchAsync<ZeroTimeoutWorkflow, SampleResult>(
            new ZeroTimeoutWorkflow(),
            Context(harness.Provider));

        Assert.True(result.Success);
        Assert.Equal(["a"], harness.Recorder.RanSteps);
    }
}
