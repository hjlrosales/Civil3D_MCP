using System.Reflection;
using Autodesk.Mcp.Sdk.Tools;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Commands;

/// <summary>
/// Base for every editing tool (Phase 5B): binds a strongly typed MCP tool to a command and runs
/// it through the <see cref="ICommandDispatcher"/> pipeline (validation, permission, confirmation,
/// write transaction, events). Builds the per-invocation <see cref="ICommandExecutionContext"/>
/// from the tool execution context, resolves the granted permission from the tool's own
/// <c>[McpTool]</c> manifest permission, and maps <see cref="CommandException"/> /
/// <see cref="DomainException"/> to protocol error codes.
/// </summary>
/// <typeparam name="TIn">Tool input DTO; must be a class with a parameterless constructor.</typeparam>
/// <typeparam name="TOut">Tool output DTO.</typeparam>
/// <typeparam name="TCommand">The command type produced from the input.</typeparam>
/// <typeparam name="TResult">The command result type.</typeparam>
public abstract class CommandToolBase<TIn, TOut, TCommand, TResult> : Civil3DToolBase<TIn, TOut>
    where TIn : class, new()
    where TOut : class
    where TCommand : class, ICommand<TResult>
{
    private readonly ICommandDispatcher _dispatcher;
    private readonly IConfirmationGate _confirmations;
    private readonly IUndoContext _undo;
    private readonly CommandPermission _grantedPermission;

    /// <summary>Creates the tool.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    /// <param name="dispatcher">The command dispatcher (full pipeline).</param>
    /// <param name="confirmations">Confirmation gate; defaults to deny (safe until wired).</param>
    /// <param name="undo">Undo context; defaults to the no-op context.</param>
    protected CommandToolBase(
        ICivil3DSession session,
        ICommandDispatcher dispatcher,
        IConfirmationGate? confirmations = null,
        IUndoContext? undo = null)
        : base(session)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _confirmations = confirmations ?? NullConfirmationGate.Instance;
        _undo = undo ?? NullUndoContext.Instance;
        _grantedPermission = ResolveGrantedPermission(GetType());
    }

    /// <summary>Builds the command from the bound tool input.</summary>
    /// <param name="input">The bound tool parameters.</param>
    /// <param name="context">Per-invocation execution context.</param>
    protected abstract TCommand CreateCommand(TIn input, ToolExecutionContext context);

    /// <summary>Maps the command result to the tool output DTO.</summary>
    /// <param name="result">The command result.</param>
    protected abstract TOut MapResult(TResult result);

    /// <inheritdoc />
    protected sealed override async Task<TOut> ExecuteToolCoreAsync(
        TIn input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActiveDrawing(context);

        TCommand command = CreateCommand(input, context);
        var commandContext = new CommandExecutionContext(
            CorrelationId: context.CorrelationId,
            SessionId: context.SessionId,
            CancellationToken: cancellationToken,
            Progress: new ToolProgressAdapter(context.Progress),
            Undo: _undo,
            EffectivePermission: _grantedPermission,
            ConfirmationGranted: _confirmations.IsGranted(command, context.CorrelationId));

        try
        {
            TResult result = await _dispatcher.DispatchAsync<TCommand, TResult>(
                command, commandContext, cancellationToken);
            return MapResult(result);
        }
        catch (CommandException ex)
        {
            throw CommandErrorMapper.Map(context, ex);
        }
        catch (DomainException ex)
        {
            throw CommandErrorMapper.Map(context, ex);
        }
    }

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
