using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// Per-execution context handed to validation rules and the engine: correlation/session identity
/// (for structured logging), a logger and the effective cancellation token. Contains no Autodesk
/// types and is never shared across executions.
/// </summary>
public interface IValidationContext
{
    /// <summary>The correlation id of the originating MCP request.</summary>
    string CorrelationId { get; }

    /// <summary>The session id of the originating MCP session.</summary>
    string SessionId { get; }

    /// <summary>Logger for rule-level diagnostics.</summary>
    ILogger Logger { get; }

    /// <summary>The effective cancellation token; rules and the engine should honour it between items.</summary>
    CancellationToken CancellationToken { get; }
}
