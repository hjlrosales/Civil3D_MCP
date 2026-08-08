using Civil3D.Domain.Commands;

namespace Civil3D.Tools.Commands;

/// <summary>
/// Decides whether the caller has already confirmed a command that requires confirmation. The
/// real integration elicits the confirmation against the MCP client via the protocol
/// ConfirmationRequest/Response flow (server-side); the bridge-side gate records the granted
/// answer per correlation id. Until that channel is wired, the null gate denies everything so a
/// dangerous command can never run unconfirmed.
/// </summary>
public interface IConfirmationGate
{
    /// <summary>Returns true when confirmation was granted for the command.</summary>
    /// <param name="command">The command awaiting confirmation.</param>
    /// <param name="correlationId">Correlation of the originating request.</param>
    bool IsGranted(ICommand command, string correlationId);
}

/// <summary>Denies all confirmations; the safe default until the confirmation channel is wired.</summary>
public sealed class NullConfirmationGate : IConfirmationGate
{
    private NullConfirmationGate() { }

    /// <summary>The shared instance.</summary>
    public static NullConfirmationGate Instance { get; } = new();

    /// <inheritdoc />
    public bool IsGranted(ICommand command, string correlationId) => false;
}
