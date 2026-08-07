using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Enums;

/// <summary>
/// The risk level associated with invoking a tool. High- and critical-risk tools are subject to
/// the confirmation flow before execution. Serialized as the exact member name (for example
/// <c>Critical</c>); unknown values fall back to <see cref="Unknown"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ToolRisk>))]
public enum ToolRisk
{
    /// <summary>Unclassified (fallback for forward compatibility).</summary>
    Unknown = 0,

    /// <summary>No lasting effect; safe to run unattended.</summary>
    Low,

    /// <summary>May change state, but reversible or low impact.</summary>
    Medium,

    /// <summary>Can meaningfully alter the drawing; confirmation recommended.</summary>
    High,

    /// <summary>Can destroy data or take very long; confirmation always required.</summary>
    Critical,
}
