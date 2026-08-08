namespace Civil3D.Domain.Commands;

/// <summary>
/// The permission a command requires from the caller. Mirrors the protocol
/// <c>ToolPermission</c> levels so the domain stays protocol-free while the tool layer can map
/// them 1:1. The dispatcher pipeline rejects a command when the granted permission is lower
/// (enum order = escalation).
/// </summary>
public enum CommandPermission
{
    /// <summary>Unclassified (falls back to ReadOnly).</summary>
    Unknown = 0,

    /// <summary>Read-only access to the drawing.</summary>
    ReadOnly,

    /// <summary>Modifies the active drawing (write transaction + document lock).</summary>
    ModifyDrawing,

    /// <summary>Exports data out of the drawing.</summary>
    Export,

    /// <summary>High-privilege administrative operations.</summary>
    Administrative,
}

/// <summary>
/// Descriptive metadata used when a command requires user confirmation. The tool layer raises
/// this against the client via the protocol confirmation request; the pipeline only enforces
/// that confirmation was granted (see <see cref="ICommandExecutionContext.ConfirmationGranted"/>).
/// </summary>
public sealed record ConfirmationDescriptor
{
    /// <summary>Short human-readable title of the pending action.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Human-readable description of what will happen if confirmed.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The risk level of the pending operation.</summary>
    public string Risk { get; init; } = "Low";

    /// <summary>Optional structured summary of the pending change.</summary>
    public string? OperationSummary { get; init; }
}

/// <summary>A command, without a result. Marker for commands whose outcome is void.</summary>
public interface ICommand
{
    /// <summary>The stable command name (for logging, events and error messages).</summary>
    string Name { get; }

    /// <summary>The permission required to execute this command.</summary>
    CommandPermission RequiredPermission { get; }

    /// <summary>True when the command only reads and must skip the write transaction.</summary>
    bool IsReadOnly { get; }

    /// <summary>True when the command is destructive/high-risk and requires explicit confirmation.</summary>
    bool RequiresConfirmation { get; }

    /// <summary>Confirmation metadata, populated when <see cref="RequiresConfirmation"/> is true.</summary>
    ConfirmationDescriptor? Confirmation { get; }
}

/// <summary>A command producing a strongly typed result.</summary>
/// <typeparam name="TResult">The immutable result DTO.</typeparam>
public interface ICommand<TResult> : ICommand
{
}
