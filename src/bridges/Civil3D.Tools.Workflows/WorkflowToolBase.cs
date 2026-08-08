using System.Reflection;
using Autodesk.Mcp.Sdk.Tools;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Workflows;

/// <summary>
/// Base for every long-running workflow tool (Phase 7A): binds a strongly typed MCP tool to a
/// workflow and runs it through the <see cref="IWorkflowDispatcher"/> pipeline (validation,
/// permission, timeout/cancellation, progress, events, structured logging). Builds the
/// per-invocation <see cref="WorkflowContext"/> from the tool execution context, resolves the
/// granted permission from the tool's own <c>[McpTool]</c> manifest permission, and maps
/// <see cref="WorkflowException"/> / <see cref="DomainException"/> to protocol error codes.
/// </summary>
/// <typeparam name="TIn">Tool input DTO; must be a class with a parameterless constructor.</typeparam>
/// <typeparam name="TOut">Tool output DTO.</typeparam>
/// <typeparam name="TWorkflow">The workflow type produced from the input.</typeparam>
/// <typeparam name="TResult">The workflow result type.</typeparam>
public abstract class WorkflowToolBase<TIn, TOut, TWorkflow, TResult> : Civil3DToolBase<TIn, TOut>
    where TIn : class, new()
    where TOut : class
    where TWorkflow : class, IWorkflow<TResult>
{
    private readonly IWorkflowDispatcher _dispatcher;
    private readonly IServiceProvider _services;
    private readonly CommandPermission _grantedPermission;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The workflow dispatcher (full pipeline).</param>
    /// <param name="services">The container, exposed to workflow steps via the context so they can
    /// resolve domain services and repositories lazily.</param>
    protected WorkflowToolBase(
        ICivil3DSession session,
        IWorkflowDispatcher dispatcher,
        IServiceProvider services)
        : base(session)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _grantedPermission = ResolveGrantedPermission(GetType());
    }

    /// <summary>Builds the workflow from the bound tool input.</summary>
    /// <param name="input">The bound tool parameters.</param>
    /// <param name="context">Per-invocation execution context.</param>
    protected abstract TWorkflow CreateWorkflow(TIn input, ToolExecutionContext context);

    /// <summary>Maps the workflow result to the tool output DTO.</summary>
    /// <param name="result">The workflow execution result.</param>
    protected abstract TOut MapResult(WorkflowResult<TResult> result);

    /// <inheritdoc />
    protected sealed override async Task<TOut> ExecuteToolCoreAsync(
        TIn input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        TWorkflow workflow = CreateWorkflow(input, context);
        var workflowContext = new WorkflowContext(
            WorkflowName: workflow.Name,
            CorrelationId: context.CorrelationId,
            SessionId: context.SessionId,
            CancellationToken: cancellationToken,
            Progress: new WorkflowProgress(new ToolProgressAdapter(context.Progress)),
            Logger: context.Logger,
            Services: _services,
            Configuration: GetConfiguration(input, context),
            EffectivePermission: _grantedPermission,
            StartedAtUtc: DateTimeOffset.UtcNow);

        try
        {
            WorkflowResult<TResult> result = await _dispatcher.DispatchAsync<TWorkflow, TResult>(
                workflow, workflowContext, cancellationToken);
            return MapResult(result);
        }
        catch (WorkflowException ex)
        {
            throw WorkflowErrorMapper.Map(context, ex);
        }
        catch (DomainException ex)
        {
            throw WorkflowErrorMapper.Map(context, ex);
        }
    }

    /// <summary>
    /// Configuration exposed to workflow steps through <see cref="WorkflowContext.Configuration"/>.
    /// Override to supply workflow settings; the default is empty.
    /// </summary>
    /// <param name="input">The bound tool parameters.</param>
    /// <param name="context">Per-invocation execution context.</param>
    protected virtual IReadOnlyDictionary<string, string> GetConfiguration(
        TIn input, ToolExecutionContext context)
        => new Dictionary<string, string>();

    /// <summary>The permission granted to this tool, taken from its own <c>[McpTool]</c> manifest.</summary>
    private static CommandPermission ResolveGrantedPermission(Type toolType)
        => toolType.GetCustomAttribute<McpToolAttribute>()?.Permission switch
        {
            Autodesk.Mcp.Shared.Enums.ToolPermission.ModifyDrawing => CommandPermission.ModifyDrawing,
            Autodesk.Mcp.Shared.Enums.ToolPermission.Export => CommandPermission.Export,
            Autodesk.Mcp.Shared.Enums.ToolPermission.Administrative => CommandPermission.Administrative,
            _ => CommandPermission.ReadOnly,
        };

    private sealed class ToolProgressAdapter : Civil3D.Domain.Commands.IProgressReporter
    {
        private readonly Autodesk.Mcp.Sdk.Tools.IProgressReporter _inner;

        public ToolProgressAdapter(Autodesk.Mcp.Sdk.Tools.IProgressReporter inner) => _inner = inner;

        public void Report(int percent, string? stage = null, string? message = null)
            => _inner.Report(percent, stage, message);
    }
}
