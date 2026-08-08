using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Enums;

namespace Autodesk.Mcp.Sdk.Tools;

/// <summary>
/// Declares the metadata of a tool. Applied to tool classes; the manifest generator reads it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>Creates the attribute with the three required display fields.</summary>
    /// <param name="name">Stable machine identifier used on the wire (for example <c>list_alignments</c>).</param>
    /// <param name="displayName">Human-friendly label.</param>
    /// <param name="description">Markdown-capable description.</param>
    public McpToolAttribute(string name, string displayName, string description)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>Stable machine identifier used on the wire.</summary>
    public string Name { get; }

    /// <summary>Human-friendly label shown to users.</summary>
    public string DisplayName { get; }

    /// <summary>Markdown-capable description of what the tool does.</summary>
    public string Description { get; }

    /// <summary>Functional category; defaults to <see cref="ToolCategory.General"/>.</summary>
    public ToolCategory Category { get; set; } = ToolCategory.General;

    /// <summary>Permission level required to invoke the tool.</summary>
    public ToolPermission Permission { get; set; } = ToolPermission.ReadOnly;

    /// <summary>Risk level associated with invoking the tool.</summary>
    public ToolRisk Risk { get; set; } = ToolRisk.Low;

    /// <summary>Semantic version of the tool contract, when specified.</summary>
    public string? Version { get; set; }

    /// <summary>Execution timeout in milliseconds.</summary>
    public int TimeoutMilliseconds { get; set; } = ProtocolConstants.DefaultToolTimeoutMilliseconds;

    /// <summary>True when the tool reports <c>$/progress</c> notifications.</summary>
    public bool SupportsProgress { get; set; }

    /// <summary>True when the tool cooperates with <c>$/cancel</c> notifications.</summary>
    public bool SupportsCancellation { get; set; }

    /// <summary>True when the tool can stream partial results.</summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>Free-form classification tags.</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>True when the tool is deprecated.</summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// True when execution must be marshaled onto the host application's main thread
    /// (required for all Autodesk API access).
    /// </summary>
    public bool RequiresApplicationContext { get; set; }
}
