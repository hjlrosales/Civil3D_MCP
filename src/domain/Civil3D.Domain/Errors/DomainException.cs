namespace Civil3D.Domain.Errors;

/// <summary>
/// The single exception type thrown by the domain layer. It carries a stable
/// <see cref="DomainErrorCode"/> so callers (services, tools) can translate failures into
/// business results or protocol responses without inspecting Autodesk exception types. The
/// underlying Autodesk failure, when present, is attached as the <see cref="Exception.InnerException"/>
/// and never exposed directly on the wire.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="code">Stable domain error code.</param>
    /// <param name="message">Human-readable failure description.</param>
    /// <param name="innerException">Optional underlying Autodesk failure (never surfaced on the wire).</param>
    public DomainException(DomainErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>The stable error code describing this failure.</summary>
    public DomainErrorCode Code { get; }
}
