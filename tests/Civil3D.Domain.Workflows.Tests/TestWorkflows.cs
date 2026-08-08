using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3D.Domain.Workflows.Tests;

/// <summary>
/// Test-only workflows, steps, handlers and validators that exercise the workflow framework.
/// These are test doubles, not production engineering workflows (those arrive in Phase 7B).
/// </summary>
internal static class TestWorkflows
{
    internal sealed record SampleResult(string Value, int StepCount);

    /// <summary>Records the names of executed steps; shared between steps and assertions.</summary>
    internal sealed class FakeRecorder
    {
        public List<string> RanSteps { get; } = [];
    }

    internal sealed class RecordStep(string name) : IWorkflowStep
    {
        public string Name => name;

        public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Services.GetRequiredService<FakeRecorder>().RanSteps.Add(name);
            return Task.FromResult(WorkflowStepOutcome.Proceed());
        }
    }

    internal sealed class StopStep(string name) : IWorkflowStep
    {
        public string Name => name;

        public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Services.GetRequiredService<FakeRecorder>().RanSteps.Add(name);
            return Task.FromResult(WorkflowStepOutcome.Stop($"stopped at {name}"));
        }
    }

    internal sealed class FailingStep : IWorkflowStep
    {
        public string Name => "failing";

        public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    internal sealed class DomainFailingStep : IWorkflowStep
    {
        public string Name => "domain-failing";

        public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
            => throw new DomainException(DomainErrorCode.EntityNotFound, "missing entity");
    }

    internal sealed class SlowStep : IWorkflowStep
    {
        public string Name => "slow";

        public async Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
        {
            for (int i = 0; i < 200; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken);
            }

            return WorkflowStepOutcome.Proceed();
        }
    }

    /// <summary>A read-only workflow with three steps and a validated input value.</summary>
    internal sealed class SampleWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "sample.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new RecordStep("a"), new RecordStep("b"), new RecordStep("c")];
        public string? Value { get; init; }
    }

    /// <summary>A workflow whose middle step stops the run early.</summary>
    internal sealed class StopAfterStepWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "stop.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new RecordStep("a"), new StopStep("b"), new RecordStep("c")];
    }

    /// <summary>A workflow requiring more than the caller's granted permission.</summary>
    internal sealed class ModifyPermissionWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "modify.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new RecordStep("a")];
    }

    /// <summary>A workflow whose step throws an unexpected exception (StepFailed path).</summary>
    internal sealed class FailingStepWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "fail.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new FailingStep()];
    }

    /// <summary>A workflow whose step throws a domain failure (pass-through path).</summary>
    internal sealed class DomainFailStepWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "domain-fail.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new DomainFailingStep()];
    }

    /// <summary>A workflow with a short timeout over a slow step (timeout path).</summary>
    internal sealed class TimeoutWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "timeout.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => TimeSpan.FromMilliseconds(50);
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new SlowStep()];
    }

    /// <summary>A fast workflow with a non-positive timeout (default fallback path).</summary>
    internal sealed class ZeroTimeoutWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "zero-timeout.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => TimeSpan.Zero;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new RecordStep("a")];
    }

    /// <summary>A slow workflow without a timeout (caller-cancellation path).</summary>
    internal sealed class CancellableWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "cancellable.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new SlowStep()];
    }

    /// <summary>A workflow with no registered handler (missing-handler path).</summary>
    internal sealed class UnregisteredWorkflow : IWorkflow<SampleResult>
    {
        public string Name => "unregistered.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new RecordStep("a")];
    }

    /// <summary>Generic test handler: runs the steps, then reports the workflow's step count.</summary>
    internal sealed class GenericHandler<TWorkflow> : WorkflowHandlerBase<TWorkflow, SampleResult>
        where TWorkflow : IWorkflow<SampleResult>
    {
        protected override Task<SampleResult> ProduceResultAsync(
            TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
            => Task.FromResult(new SampleResult("done", workflow.Steps.Count));
    }

    internal sealed class ValueRequiredValidator : IWorkflowValidator<SampleWorkflow>
    {
        public ValidationResult Validate(SampleWorkflow workflow)
            => string.IsNullOrWhiteSpace(workflow.Value)
                ? ValidationResult.Invalid(new ValidationFailure("Value", "Value must not be empty."))
                : ValidationResult.Valid;
    }

    internal sealed class ValueMaxLengthValidator : IWorkflowValidator<SampleWorkflow>
    {
        public ValidationResult Validate(SampleWorkflow workflow)
            => workflow.Value is null or { Length: > 10 }
                ? ValidationResult.Invalid(new ValidationFailure("Value", "Value must be at most 10 characters."))
                : ValidationResult.Valid;
    }
}
