namespace Civil3D.Domain.Commands;

/// <summary>
/// The single exception type thrown by the command framework. Carries a stable
/// <see cref="CommandErrorCode"/> so the tool layer can translate failures into protocol
/// responses without inspecting framework internals. The underlying failure, when present, is
/// attached as <see cref="Exception.InnerException"/> and never exposed on the wire.
/// </summary>
public sealed class CommandException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="code">Stable command error code.</param>
    /// <param name="message">Human-readable failure description.</param>
    /// <param name="innerException">Optional underlying failure (never surfaced on the wire).</param>
    public CommandException(CommandErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>The stable error code describing this failure.</summary>
    public CommandErrorCode Code { get; }
}
