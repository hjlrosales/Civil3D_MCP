namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable read-only snapshot of a pipe within a pipe network.
/// </summary>
public sealed record PipeInfo
{
    /// <summary>Stable numeric id derived from the pipe's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The pipe name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The pipe description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>Id of the network that owns the pipe.</summary>
    public long NetworkId { get; init; }

    /// <summary>The station at the start of the pipe.</summary>
    public double StartStation { get; init; }

    /// <summary>The station at the end of the pipe.</summary>
    public double EndStation { get; init; }
}
