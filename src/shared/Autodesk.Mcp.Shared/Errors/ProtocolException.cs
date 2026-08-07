namespace Autodesk.Mcp.Shared.Errors;

/// <summary>
/// Raised when a wire message violates the protocol contract (malformed JSON-RPC, unknown method,
/// version mismatch, unexpected payload shape). Maps to <see cref="ErrorCode.E_INVALID_REQUEST"/>.
/// </summary>
public sealed class ProtocolException : BridgeException
{
    /// <summary>Creates a protocol exception.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ProtocolException(string message, string? correlationId = null, Exception? innerException = null)
        : base(ErrorCode.E_INVALID_REQUEST, message, correlationId, innerException: innerException)
    {
    }
}
