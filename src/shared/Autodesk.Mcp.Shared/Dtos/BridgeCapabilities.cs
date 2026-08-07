using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// The capabilities a bridge advertises, both in its endpoint descriptor (for discovery) and
/// in the handshake response (for negotiation). Capabilities are additive: a new bridge or a
/// new bridge version may set more of them, but never removes existing semantics.
/// </summary>
public sealed record BridgeCapabilities
{
    /// <summary>True when streaming responses (partial results before completion) are supported.</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>True when the bridge emits <c>$/progress</c> notifications for long operations.</summary>
    public bool SupportsProgress { get; init; }

    /// <summary>True when in-flight tool executions can be cancelled via <c>$/cancel</c>.</summary>
    public bool SupportsCancellation { get; init; }

    /// <summary>True when the bridge participates in the confirmation flow for risky operations.</summary>
    public bool SupportsConfirmation { get; init; }

    /// <summary>True when the bridge accepts batch requests (multiple calls per pipe round trip).</summary>
    public bool SupportsBatchRequests { get; init; }

    /// <summary>True when the bridge can execute independent tools concurrently on its worker pool.</summary>
    public bool SupportsParallelExecution { get; init; }

    /// <summary>The highest protocol version this bridge supports.</summary>
    public VersionInformation SupportedProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>The product identifiers this bridge serves (for example <c>Civil3D</c>, <c>AutoCAD</c>).</summary>
    public IReadOnlyList<string> SupportedProducts { get; init; } = Array.Empty<string>();
}
