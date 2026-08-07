using System.Text.Json;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// An optional worked example attached to a <see cref="ToolManifest"/>, used by AI clients to
/// understand expected inputs and outputs without executing the tool.
/// </summary>
public sealed record ToolExample
{
    /// <summary>Short name for the example (for example <c>minimal</c>).</summary>
    public string? Name { get; init; }

    /// <summary>Optional description of what the example demonstrates.</summary>
    public string? Description { get; init; }

    /// <summary>Example input matching the tool's input schema.</summary>
    public JsonElement? Input { get; init; }

    /// <summary>Example output matching the tool's output schema.</summary>
    public JsonElement? Output { get; init; }
}
