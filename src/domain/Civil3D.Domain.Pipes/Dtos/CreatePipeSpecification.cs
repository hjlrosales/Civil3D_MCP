namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Autodesk-free description of a pipe to create, resolved by the tool layer from the request:
/// a target network, a search string used to resolve the pipe part family from that network's
/// parts list, a nominal diameter, and start/end points. Consumed by <c>ICreatePipeService</c>
/// and <c>IPipeCreateRepository</c>.
/// </summary>
public sealed record CreatePipeSpecification
{
    /// <summary>Name of the existing pipe network to add the pipe to.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>
    /// Text matched (case-insensitive, substring) against the description of every pipe part
    /// family already assigned to the network's parts list. Must match exactly one family.
    /// </summary>
    public string PartFamilyMatch { get; init; } = string.Empty;

    /// <summary>Target nominal/inner diameter in millimeters; snapped to the closest available size.</summary>
    public double DiameterMm { get; init; }

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

    /// <summary>Optional free-text description to set on the created pipe.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional relaxed match text (usually the bare material, for example "HDPE") retried when
    /// the strict <see cref="PartFamilyMatch"/> matches no family — so material/rating prompts
    /// such as "HDPE SDR17 PN10" still resolve when the drawing's catalog names families without
    /// the rating (for example "HDPE Pipe SI"). Never set for explicit user-supplied matches.
    /// </summary>
    public string? FallbackMatch { get; init; }
}
