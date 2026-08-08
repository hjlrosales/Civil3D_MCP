using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Enums;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3D.Tools.Workflows.Tests;

/// <summary>
/// Test-only workflows, steps, handlers, validators and MCP tools that exercise the workflow
/// tool base. These are test doubles, not production engineering workflows (Phase 7B).
/// </summary>
internal static class TestTools
{
    internal sealed record ReportResult(string Value, int StepCount);

    /// <summary>In-memory store the test step writes to (stands in for a domain service).</summary>
    internal sealed class FakeStore
    {
        public List<string> Entries { get; } = [];
    }

    internal sealed class StoreStep : IWorkflowStep
    {
        public string Name => "store";

        public Task<WorkflowStepOutcome> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Services.GetRequiredService<FakeStore>().Entries.Add("ran");
            return Task.FromResult(WorkflowStepOutcome.Proceed());
        }
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
            => throw new DomainException(DomainErrorCode.EntityNotFound, "missing surface");
    }

    /// <summary>A read-only report workflow with one step and a validated input value.</summary>
    internal sealed class ReportWorkflow : IWorkflow<ReportResult>
    {
        public string Name => "report.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new StoreStep()];
        public string? Value { get; init; }
    }

    /// <summary>A workflow requiring more permission than the tool manifest grants.</summary>
    internal sealed class DeniedWorkflow : IWorkflow<ReportResult>
    {
        public string Name => "denied.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new StoreStep()];
    }

    /// <summary>A workflow with a short timeout over a slow step (timeout path).</summary>
    internal sealed class TimeoutWorkflow : IWorkflow<ReportResult>
    {
        public string Name => "timeout.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => TimeSpan.FromMilliseconds(50);
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new SlowStep()];
    }

    /// <summary>A workflow whose step throws an unexpected exception (internal-error path).</summary>
    internal sealed class FailingWorkflow : IWorkflow<ReportResult>
    {
        public string Name => "failing.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new FailingStep()];
    }

    /// <summary>A workflow whose step throws a domain failure (object-not-found path).</summary>
    internal sealed class DomainFailWorkflow : IWorkflow<ReportResult>
    {
        public string Name => "domain-fail.workflow";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public TimeSpan? Timeout => null;
        public IReadOnlyList<IWorkflowStep> Steps { get; } = [new DomainFailingStep()];
    }

    /// <summary>Generic test handler: runs the steps, then reports the workflow's step count.</summary>
    internal sealed class GenericHandler<TWorkflow> : WorkflowHandlerBase<TWorkflow, ReportResult>
        where TWorkflow : IWorkflow<ReportResult>
    {
        protected override Task<ReportResult> ProduceResultAsync(
            TWorkflow workflow, IWorkflowContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ReportResult("done", workflow.Steps.Count));
    }

    internal sealed class ValueRequiredValidator : IWorkflowValidator<ReportWorkflow>
    {
        public ValidationResult Validate(ReportWorkflow workflow)
            => string.IsNullOrWhiteSpace(workflow.Value)
                ? ValidationResult.Invalid(new ValidationFailure("Value", "Value must not be empty."))
                : ValidationResult.Valid;
    }

    internal sealed class ReportInput
    {
        public string? Value { get; set; }
    }

    [McpTool("test_report", "Test Report Workflow", "Test workflow tool used by the Phase 7A framework tests.",
        Category = ToolCategory.General, Permission = ToolPermission.ReadOnly)]
    internal sealed class ReportWorkflowTool : WorkflowToolBase<ReportInput, ReportResult, ReportWorkflow, ReportResult>
    {
        public ReportWorkflowTool(ICivil3DSession session, IWorkflowDispatcher dispatcher, IServiceProvider services)
            : base(session, dispatcher, services)
        {
        }

        protected override ReportWorkflow CreateWorkflow(ReportInput input, ToolExecutionContext context)
            => new() { Value = input.Value };

        protected override ReportResult MapResult(WorkflowResult<ReportResult> result) => result.Data;
    }

    /// <summary>Read-only manifest, but its workflow requires ModifyDrawing (permission-denied path).</summary>
    [McpTool("test_denied", "Denied Workflow", "Workflow tool that requires more permission than the manifest grants.",
        Category = ToolCategory.General, Permission = ToolPermission.ReadOnly)]
    internal sealed class DeniedWorkflowTool : WorkflowToolBase<ReportInput, ReportResult, DeniedWorkflow, ReportResult>
    {
        public DeniedWorkflowTool(ICivil3DSession session, IWorkflowDispatcher dispatcher, IServiceProvider services)
            : base(session, dispatcher, services)
        {
        }

        protected override DeniedWorkflow CreateWorkflow(ReportInput input, ToolExecutionContext context) => new();

        protected override ReportResult MapResult(WorkflowResult<ReportResult> result) => result.Data;
    }

    [McpTool("test_timeout", "Timeout Workflow", "Workflow that exceeds its execution timeout.",
        Category = ToolCategory.General, Permission = ToolPermission.ReadOnly)]
    internal sealed class TimeoutWorkflowTool : WorkflowToolBase<ReportInput, ReportResult, TimeoutWorkflow, ReportResult>
    {
        public TimeoutWorkflowTool(ICivil3DSession session, IWorkflowDispatcher dispatcher, IServiceProvider services)
            : base(session, dispatcher, services)
        {
        }

        protected override TimeoutWorkflow CreateWorkflow(ReportInput input, ToolExecutionContext context) => new();

        protected override ReportResult MapResult(WorkflowResult<ReportResult> result) => result.Data;
    }

    [McpTool("test_failing", "Failing Workflow", "Workflow whose step throws an unexpected exception.",
        Category = ToolCategory.General, Permission = ToolPermission.ReadOnly)]
    internal sealed class FailingWorkflowTool : WorkflowToolBase<ReportInput, ReportResult, FailingWorkflow, ReportResult>
    {
        public FailingWorkflowTool(ICivil3DSession session, IWorkflowDispatcher dispatcher, IServiceProvider services)
            : base(session, dispatcher, services)
        {
        }

        protected override FailingWorkflow CreateWorkflow(ReportInput input, ToolExecutionContext context) => new();

        protected override ReportResult MapResult(WorkflowResult<ReportResult> result) => result.Data;
    }

    [McpTool("test_domainfail", "Domain Fail Workflow", "Workflow whose step throws a domain failure.",
        Category = ToolCategory.General, Permission = ToolPermission.ReadOnly)]
    internal sealed class DomainFailWorkflowTool : WorkflowToolBase<ReportInput, ReportResult, DomainFailWorkflow, ReportResult>
    {
        public DomainFailWorkflowTool(ICivil3DSession session, IWorkflowDispatcher dispatcher, IServiceProvider services)
            : base(session, dispatcher, services)
        {
        }

        protected override DomainFailWorkflow CreateWorkflow(ReportInput input, ToolExecutionContext context) => new();

        protected override ReportResult MapResult(WorkflowResult<ReportResult> result) => result.Data;
    }
}
