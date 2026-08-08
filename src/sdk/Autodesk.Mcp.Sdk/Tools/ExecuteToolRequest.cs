using System.Text.Json;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// The wire payload of a <c>tools/execute</c> request: the tool name plus its arguments.
/// </summary>
public sealed record ExecuteToolRequest
{
    /// <summary>The name of the tool to execute.</summary>
    public string? Tool { get; init; }

    /// <summary>Raw tool arguments.</summary>
    public JsonElement? Arguments { get; init; }

    /// <summary>Optional execution timeout override in milliseconds.</summary>
    public long? TimeoutMs { get; init; }

    /// <summary>Explicit confirmation flag for editing tools (reserved; policy enforced server-side).</summary>
    public bool? Confirm { get; init; }
}
