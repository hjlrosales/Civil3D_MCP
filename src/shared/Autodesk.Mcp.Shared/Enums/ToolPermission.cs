using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Enums;

/// <summary>
/// The permission level a tool requires, declared in its manifest. Enforcement lives in the MCP
/// server policy layer; the bridge re-checks the declared level against the operation type
/// (defense in depth). Serialized as the exact member name (for example <c>ModifyDrawing</c>).
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ToolPermission>))]
public enum ToolPermission
{
    /// <summary>Unclassified (fallback for forward compatibility).</summary>
    Unknown = 0,

    /// <summary>Read-only access to the drawing and its objects.</summary>
    ReadOnly,

    /// <summary>Modifies the active drawing (requires transaction + document lock).</summary>
    ModifyDrawing,

    /// <summary>Exports data out of the drawing (files, reports).</summary>
    Export,

    /// <summary>High-privilege administrative operations (styles, settings, regeneration).</summary>
    Administrative,
}
