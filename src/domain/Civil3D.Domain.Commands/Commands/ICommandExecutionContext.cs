namespace Civil3D.Domain.Commands;

/// <summary>
/// Per-command execution context handed to validators, handlers and the pipeline: correlation and
/// session identifiers, the effective cancellation token, progress reporting, the undo context,
/// the granted permission level and whether required confirmation was granted. A fresh instance
/// is created for every dispatch.
/// </summary>
public interface ICommandExecutionContext
{
    /// <summary>Correlation identifier of the originating request.</summary>
    string CorrelationId { get; }

    /// <summary>Session identifier, when the request carried one.</summary>
    string? SessionId { get; }

    /// <summary>Effective cancellation token (client cancellation, timeout, shutdown).</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Progress reporter for the command.</summary>
    IProgressReporter Progress { get; }

    /// <summary>Undo context for future AutoCAD undo integration.</summary>
    IUndoContext Undo { get; }

    /// <summary>The permission granted to the caller; the pipeline rejects commands above this level.</summary>
    CommandPermission EffectivePermission { get; }

    /// <summary>True when the caller has already confirmed the operation (for commands that require it).</summary>
    bool ConfirmationGranted { get; }
}

/// <summary>The standard immutable implementation of <see cref="ICommandExecutionContext"/>.</summary>
public sealed record CommandExecutionContext(
    string CorrelationId,
    string? SessionId,
    CancellationToken CancellationToken,
    IProgressReporter Progress,
    IUndoContext Undo,
    CommandPermission EffectivePermission,
    bool ConfirmationGranted) : ICommandExecutionContext;
