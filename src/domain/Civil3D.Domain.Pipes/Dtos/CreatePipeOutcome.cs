namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// The outcome of a pipe creation performed by a write repository: everything read back from the
/// newly created Autodesk part. Autodesk-free; produced inside the write transaction.
/// </summary>
public sealed record CreatePipeOutcome
{
    /// <summary>Stable numeric id of the created pipe.</summary>
    public long PipeId { get; init; }

    /// <summary>The auto-generated pipe name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Stable numeric id of the owning network.</summary>
    public long NetworkId { get; init; }

    /// <summary>Name of the owning network.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>Description of the matched pipe part family.</summary>
    public string PartFamilyName { get; init; } = string.Empty;

    /// <summary>Name of the selected part size.</summary>
    public string PartSizeName { get; init; } = string.Empty;

    /// <summary>The part's material, or <see langword="null"/> when not set.</summary>
    public string? Material { get; init; }

    /// <summary>Inner diameter or width, in drawing units (meters).</summary>
    public double InnerDiameterOrWidth { get; init; }

    /// <summary>Outer diameter or width, in drawing units (meters).</summary>
    public double OuterDiameterOrWidth { get; init; }

    /// <summary>Start point easting.</summary>
    public double StartEasting { get; init; }

    /// <summary>Start point northing.</summary>
    public double StartNorthing { get; init; }

    /// <summary>Start point elevation.</summary>
    public double StartElevation { get; init; }

    /// <summary>End point easting.</summary>
    public double EndEasting { get; init; }

    /// <summary>End point northing.</summary>
    public double EndNorthing { get; init; }

    /// <summary>End point elevation.</summary>
    public double EndElevation { get; init; }

    /// <summary>Center-to-center 3D length of the pipe.</summary>
    public double Length3D { get; init; }
}
