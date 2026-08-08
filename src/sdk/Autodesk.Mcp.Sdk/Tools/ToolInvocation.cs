using System.Text.Json;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>A request to execute a named tool with raw parameters.</summary>
public sealed record ToolInvocation
{
    /// <summary>The tool name to execute.</summary>
    public required string ToolName { get; init; }

    /// <summary>Raw input parameters, or null when absent.</summary>
    public JsonElement? Parameters { get; init; }

    /// <summary>Correlation identifier of the originating request.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Session identifier of the originating request.</summary>
    public string? SessionId { get; init; }

    /// <summary>Execution timeout override in milliseconds; falls back to the manifest value.</summary>
    public long? TimeoutMilliseconds { get; init; }

    /// <summary>Queue priority; lower values execute sooner (default 0). Reserved for future prioritization.</summary>
    public int Priority { get; init; }
}
