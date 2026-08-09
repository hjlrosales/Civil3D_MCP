namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable outcome of a create-pipe-network command. Autodesk-free and serializable; the tool
/// layer maps it directly to the protocol response.
/// </summary>
public sealed record CreatePipeNetworkResult
{
    /// <summary>Stable numeric id of the created network.</summary>
    public long NetworkId { get; init; }

    /// <summary>The network name (may be adjusted by Civil 3D for uniqueness).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The network description, when one was set.</summary>
    public string? Description { get; init; }

    /// <summary>Name of the parts list assigned to the network.</summary>
    public string PartsListName { get; init; } = string.Empty;

    /// <summary>Catalog descriptions of the pipe part families actually added to the parts list.</summary>
    public IReadOnlyList<string> FamiliesAdded { get; init; } = Array.Empty<string>();

    /// <summary>Requested materials whose part families could not be added from the catalog.</summary>
    public IReadOnlyList<string> FamiliesFailed { get; init; } = Array.Empty<string>();

    /// <summary>True when the network was created (always true on a successful command).</summary>
    public bool Success { get; init; }

    /// <summary>UTC timestamp of the creation.</summary>
    public DateTime TimestampUtc { get; init; }
}
