namespace Civil3D.Tools.Editing.Dtos;

/// <summary>
/// Input of <c>update_pipe</c>: the stable pipe id plus the optional changes to apply (elevation
/// of both ends, horizontal length along the pipe's current bearing, inner diameter). At least
/// one change must be provided; omitted changes keep the pipe's current value.
/// </summary>
public sealed record UpdatePipeRequest
{
    /// <summary>
    /// Stable numeric id of the pipe to update, as returned by <c>create_pipe</c> or
    /// <c>list_pipe_networks</c>.
    /// </summary>
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
