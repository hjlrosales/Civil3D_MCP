using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>Per-request routing context derived from the incoming envelope.</summary>
public sealed record RpcContext
{
    /// <summary>The JSON-RPC request id; null for notifications.</summary>
    public JsonRpcId? RequestId { get; init; }

    /// <summary>Correlation identifier of the request, when provided.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Session identifier of the request, when provided.</summary>
    public string? SessionId { get; init; }

    /// <summary>True when the request is a JSON-RPC notification (no response expected).</summary>
    public bool IsNotification => RequestId is null;
}
