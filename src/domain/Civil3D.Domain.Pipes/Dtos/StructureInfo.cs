namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable read-only snapshot of a structure within a pipe network.
/// </summary>
public sealed record StructureInfo
{
    /// <summary>Stable numeric id derived from the structure's database handle.</summary>
    public long Id { get; init; }

    /// <summary>The structure name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The structure description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>Id of the network that owns the structure.</summary>
    public long NetworkId { get; init; }

    /// <summary>The structure insertion easting.</summary>
    public double Easting { get; init; }

    /// <summary>The structure insertion northing.</summary>
    public double Northing { get; init; }

    /// <summary>The rim elevation of the structure.</summary>
    public double RimElevation { get; init; }

    /// <summary>The sump elevation of the structure.</summary>
    public double SumpElevation { get; init; }
}
