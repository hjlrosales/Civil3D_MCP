using System.ComponentModel.DataAnnotations;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Dtos;

/// <summary>
/// The complete tool catalog returned by <c>tools/list</c>. The MCP server diff-caches this catalog
/// and re-registers MCP tools only when it changes (for example after a bridge restart).
/// </summary>
public sealed record Manifest
{
    /// <summary>Schema version of this manifest document; bumped when the manifest shape changes.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>The protocol version under which this catalog was produced.</summary>
    public VersionInformation ProtocolVersion { get; init; } = VersionInformation.Empty;

    /// <summary>UTC timestamp at which the catalog was generated.</summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>All tools served by this bridge.</summary>
    [Required]
    public IReadOnlyList<ToolManifest> Tools { get; init; } = Array.Empty<ToolManifest>();
}
