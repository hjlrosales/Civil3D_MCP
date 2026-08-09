namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// The outcome of a pipe-network creation performed by a write repository: the network identity
/// plus the parts list and the families that were (or could not be) added. Autodesk-free;
/// produced inside the write transaction.
/// </summary>
public sealed record CreatePipeNetworkOutcome
{
    /// <summary>Stable numeric id of the created network.</summary>
    public long NetworkId { get; init; }

    /// <summary>The network name (may be adjusted by Civil 3D for uniqueness).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Name of the parts list assigned to the network.</summary>
    public string PartsListName { get; init; } = string.Empty;

    /// <summary>Catalog descriptions of the pipe part families actually added to the parts list.</summary>
    public IReadOnlyList<string> FamiliesAdded { get; init; } = Array.Empty<string>();

    /// <summary>Requested materials whose part families could not be added from the catalog.</summary>
    public IReadOnlyList<string> FamiliesFailed { get; init; } = Array.Empty<string>();
}
