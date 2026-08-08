using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Sdk.Tools;
using NJsonSchema;

namespace Autodesk.Mcp.Sdk.Discovery;

/// <summary>
/// Read-only view over the discovered tools, their cached manifests and their input schemas.
/// </summary>
public interface IToolCatalog
{
    /// <summary>All tool manifests, in discovery order.</summary>
    IReadOnlyList<ToolManifest> Manifests { get; }

    /// <summary>All tool names.</summary>
    IReadOnlyCollection<string> ToolNames { get; }

    /// <summary>Gets a tool by name.</summary>
    bool TryGetTool(string name, out ITool tool);

    /// <summary>Gets the manifest of a tool by name.</summary>
    ToolManifest? GetManifest(string name);

    /// <summary>Gets the cached input JSON Schema of a tool, for request validation.</summary>
    JsonSchema? GetInputSchema(string name);
}
