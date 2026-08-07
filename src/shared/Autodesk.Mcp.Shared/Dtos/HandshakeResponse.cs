using System.ComponentModel.DataAnnotations;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// The bridge's answer to a <see cref="HandshakeRequest"/>. On success it carries the agreed
/// protocol version, the newly issued session, and the full bridge information.
/// On rejection the bridge returns the standard error envelope instead of this payload.
/// </summary>
public sealed record HandshakeResponse
{
    /// <summary>The protocol version agreed upon for this connection (the lower of client and bridge).</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>The session identifier the client must echo on every subsequent request.</summary>
    [Required]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Descriptive metadata about the bridge that accepted the connection.</summary>
    public BridgeInformation? Bridge { get; init; }

    /// <summary>Optional human-readable note (for example about deprecated features in use).</summary>
    public string? Message { get; init; }
}
