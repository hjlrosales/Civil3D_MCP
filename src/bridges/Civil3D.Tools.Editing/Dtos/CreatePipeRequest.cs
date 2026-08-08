namespace Civil3D.Tools.Editing.Dtos;

/// <summary>
/// Input of <c>create_pipe</c>: the target network, the criteria used to resolve the pipe part
/// (material/SDR/pressure class, or an explicit part family match), the target diameter, and the
/// horizontal placement (start point, length and direction).
/// </summary>
public sealed record CreatePipeRequest
{
    /// <summary>Name of the existing pipe network to add the pipe to.</summary>
    public string NetworkName { get; init; } = string.Empty;

    /// <summary>Pipe material (for example "HDPE", "PVC", "Concrete").</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary>Standard Dimension Ratio, when the material is rated by SDR (for example "17").</summary>
    public string? Sdr { get; init; }

    /// <summary>Nominal pressure rating in bar, when the material is pressure-rated (for example 10 for PN10).</summary>
    public double? PressureClassBar { get; init; }

    /// <summary>
    /// Overrides the automatic <c>Material</c>/<c>Sdr</c>/<c>PressureClassBar</c>-derived search
    /// text with an explicit match against the network's pipe part family descriptions.
    /// </summary>
    public string? PartFamilyMatch { get; init; }

    /// <summary>Target nominal/inner diameter in millimeters; snapped to the closest available size.</summary>
    public double DiameterMm { get; init; }

    /// <summary>Horizontal run length in meters.</summary>
    public double LengthMeters { get; init; }

    /// <summary>Plan direction in degrees, measured counter-clockwise from the +Easting axis. Defaults to 0.</summary>
    public double DirectionDegrees { get; init; }

    /// <summary>Start point easting.</summary>
    public double StartEasting { get; init; }

    /// <summary>Start point northing.</summary>
    public double StartNorthing { get; init; }

    /// <summary>Start point elevation. The pipe runs horizontally, so the end point shares this elevation.</summary>
    public double StartElevation { get; init; }

    /// <summary>Optional free-text description to set on the created pipe.</summary>
    public string? Description { get; init; }
}
