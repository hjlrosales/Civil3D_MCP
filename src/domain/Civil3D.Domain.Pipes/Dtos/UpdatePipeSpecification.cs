namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Autodesk-free description of an update applied to an existing pipe, resolved by the tool layer
/// from the request: the stable pipe id plus the optional changes (elevation of both ends,
/// horizontal length along the pipe's current bearing, and inner diameter). At least one change
/// must be set — enforced by the structural validator.
/// </summary>
public sealed record UpdatePipeSpecification
{
    /// <summary>Stable numeric id of the pipe to update.</summary>
    public long PipeId { get; init; }

    /// <summary>
    /// When set, both the start and end elevation are set to this value (the pipe runs
    /// horizontally at the new elevation).
    /// </summary>
    public double? ElevationMeters { get; init; }

    /// <summary>
    /// When set, the end point is moved along the pipe's current horizontal bearing so the
    /// center-to-center length becomes this value; the start point stays fixed and the end
    /// elevation is preserved.
    /// </summary>
    public double? LengthMeters { get; init; }

    /// <summary>
    /// When set, the pipe is resized to the available part size closest to this nominal inner
    /// diameter (millimetres).
    /// </summary>
    public double? DiameterMm { get; init; }
}
