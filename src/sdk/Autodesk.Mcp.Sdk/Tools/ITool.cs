using System.Text.Json;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// Contract every tool implements. Metadata lives on <see cref="McpToolAttribute"/>; the discovery
/// layer turns it into a <see cref="Autodesk.Mcp.Shared.Dtos.ToolManifest"/>.
/// </summary>
public interface ITool
{
    /// <summary>Stable machine identifier of the tool (matches the manifest name).</summary>
    string Name { get; }

    /// <summary>The DTO type used to bind and validate input parameters.</summary>
    Type InputType { get; }

    /// <summary>The DTO type of the tool result.</summary>
    Type OutputType { get; }

    /// <summary>True when execution must run on the host application's main thread.</summary>
    bool RequiresApplicationContext { get; }

    /// <summary>Executes the tool.</summary>
    /// <param name="context">Per-invocation context (correlation, session, cancellation, logging).</param>
    /// <param name="parameters">Raw input parameters, or null when absent.</param>
    Task<object?> ExecuteAsync(ToolExecutionContext context, JsonElement? parameters);
}
