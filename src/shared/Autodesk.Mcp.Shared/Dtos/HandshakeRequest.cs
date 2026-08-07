using System.ComponentModel.DataAnnotations;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// The payload of the <c>handshake</c> method sent by the client to the bridge.
/// The bridge refuses connections whose protocol major version it does not support.
/// </summary>
public sealed record HandshakeRequest
{
    /// <summary>The semantic protocol version the client speaks. <c>0.0.0</c> means not provided.</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>Name of the connecting client (for example <c>Autodesk.MCP.Server</c>).</summary>
    [Required]
    public string ClientName { get; init; } = string.Empty;

    /// <summary>Version of the connecting client, when known.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>
    /// Reserved from day one for future authentication. When the bridge is configured to require
    /// a token, a missing or invalid token is answered with <c>E_PERMISSION_DENIED</c>.
    /// </summary>
    public string? AuthenticationToken { get; init; }

    /// <summary>Optional capabilities the client supports.</summary>
    public ClientCapabilities? Capabilities { get; init; }
}
