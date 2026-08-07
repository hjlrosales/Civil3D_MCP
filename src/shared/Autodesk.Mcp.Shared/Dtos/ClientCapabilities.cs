namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// Optional capabilities advertised by the client (the MCP server) during the handshake.
/// The bridge uses these to decide, for example, whether confirmation can be elicited
/// from the client rather than rejected with <c>E_CONFIRMATION_REQUIRED</c>.
/// </summary>
public sealed record ClientCapabilities
{
    /// <summary>True when the client can elicit and answer confirmation prompts.</summary>
    public bool SupportsConfirmation { get; init; }

    /// <summary>True when the client can consume <c>$/progress</c> notifications.</summary>
    public bool SupportsProgress { get; init; }

    /// <summary>True when the client can send <c>$/cancel</c> notifications.</summary>
    public bool SupportsCancellation { get; init; }

    /// <summary>True when the client can batch multiple requests into one pipe round trip.</summary>
    public bool SupportsBatchRequests { get; init; }

    /// <summary>True when the client can run independent tools concurrently.</summary>
    public bool SupportsParallelExecution { get; init; }
}
