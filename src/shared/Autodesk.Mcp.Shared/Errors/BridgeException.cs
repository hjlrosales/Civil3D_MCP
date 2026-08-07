namespace Autodesk.Mcp.Shared.Errors;

/// <summary>
/// The base exception for all protocol-level failures raised inside the shared layer, the SDK and
/// the bridge. Carries a stable <see cref="ErrorCode"/> plus optional correlation/session context.
/// Bridge code must never let a raw <see cref="BridgeException"/> cross the pipe; it is mapped to a
/// <c>ResponseEnvelope</c> or <c>ErrorEnvelope</c> instead.
/// </summary>
public class BridgeException : Exception
{
    /// <summary>Creates a bridge exception.</summary>
    /// <param name="errorCode">The stable error code.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="sessionId">Optional session identifier.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public BridgeException(ErrorCode errorCode, string message, string? correlationId = null, string? sessionId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        CorrelationId = correlationId;
        SessionId = sessionId;
    }

    /// <summary>Creates a bridge exception with a default <see cref="ErrorCode.E_INTERNAL"/> code.</summary>
    /// <param name="message">The failure message.</param>
    public BridgeException(string message)
        : this(ErrorCode.E_INTERNAL, message)
    {
    }

    /// <summary>The stable error code associated with this failure.</summary>
    public ErrorCode ErrorCode { get; }

    /// <summary>The correlation identifier of the failing operation, when known.</summary>
    public string? CorrelationId { get; }

    /// <summary>The session identifier of the failing operation, when known.</summary>
    public string? SessionId { get; }
}
