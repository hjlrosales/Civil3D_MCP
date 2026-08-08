namespace Civil3D.Tools.CutFill.Abstractions;

/// <summary>
/// The raw output of a volume calculation. A <see cref="CutFillStatus.NotSupported"/> result
/// carries <c>NotSupportedReason</c> and zero volumes rather than invented numbers. Volumes are
/// positive magnitudes; <c>NetVolume</c> is signed (positive = net cut / export, negative =
/// net fill / import). Immutable.
/// </summary>
public sealed record CutFillCalculationResult
{
    /// <summary>Whether volumes were computed.</summary>
    public CutFillStatus Status { get; init; } = CutFillStatus.NotSupported;

    /// <summary>Why volumes could not be computed; null when computed.</summary>
    public string? NotSupportedReason { get; init; }

    /// <summary>The cut (excavation) volume in cubic drawing units; ≥ 0.</summary>
    public double CutVolume { get; init; }

    /// <summary>The fill (embankment) volume in cubic drawing units; ≥ 0.</summary>
    public double FillVolume { get; init; }

    /// <summary>The signed net volume (cut − fill); positive = net cut.</summary>
    public double NetVolume { get; init; }

    /// <summary>The surface area used for the calculation, in square drawing units.</summary>
    public double SurfaceAreaUsed { get; init; }
}
