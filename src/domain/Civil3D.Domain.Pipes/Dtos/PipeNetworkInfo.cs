namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable read-only snapshot of a Civil 3D pipe network, including its parts.
/// Contains only serializable types; parts are referenced by id.
/// </summary>
public sealed record PipeNetworkInfo
{
    /// <summary>Stable numeric id derived from the network's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The network name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The network description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>The parts list used by the network, or <see langword="null"/> when none.</summary>
    public string? PartsListName { get; init; }

    /// <summary>The pipes in the network.</summary>
    public IReadOnlyList<PipeInfo> Pipes { get; init; } = Array.Empty<PipeInfo>();

    /// <summary>The structures in the network.</summary>
    public IReadOnlyList<StructureInfo> Structures { get; init; } = Array.Empty<StructureInfo>();

    /// <summary>The number of pipes in the network.</summary>
    public int PipeCount => Pipes.Count;

    /// <summary>The number of structures in the network.</summary>
    public int StructureCount => Structures.Count;
}
