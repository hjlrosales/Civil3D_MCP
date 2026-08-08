using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// Per-invocation context handed to a tool: correlation/session identifiers, the effective
/// cancellation token (timeout- and <c>$/cancel</c>-linked), logging and progress facilities.
/// A fresh instance is created for every execution.
/// </summary>
public sealed class ToolExecutionContext
{
    /// <summary>The name of the tool being executed.</summary>
    public required string ToolName { get; init; }

    /// <summary>Correlation identifier of the originating request.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Session identifier, when the request carried one.</summary>
    public string? SessionId { get; init; }

    /// <summary>Effective cancellation token (client cancellation, timeout, shutdown).</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Logger scoped to the tool; entries carry correlation context via the caller.</summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;

    /// <summary>Progress reporter; currently a no-op until pipe-backed progress lands.</summary>
    public IProgressReporter Progress { get; init; } = NullProgressReporter.Instance;
}
