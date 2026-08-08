using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// Default immutable <see cref="IValidationContext"/> for one validation run.
/// </summary>
/// <param name="CorrelationId">The correlation id of the originating MCP request.</param>
/// <param name="SessionId">The session id of the originating MCP session.</param>
/// <param name="CancellationToken">The effective cancellation token.</param>
/// <param name="Logger">Logger for rule-level diagnostics; a null logger when omitted.</param>
public sealed record ValidationContext(
    string CorrelationId,
    string SessionId,
    CancellationToken CancellationToken,
    ILogger? Logger = null) : IValidationContext
{
    /// <inheritdoc />
    public ILogger Logger { get; } = Logger ?? NullLogger.Instance;
}
