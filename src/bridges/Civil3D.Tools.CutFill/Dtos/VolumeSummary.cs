using Civil3D.Tools.CutFill.Abstractions;

namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// The headline of the cut/fill report: the identities of both surfaces, the computed volumes
/// (or the not-supported status with its reason), the surface area used and an overall verdict.
/// </summary>
public sealed record VolumeSummary
{
    /// <summary>The id of the existing ground surface.</summary>
    public long ExistingSurfaceId { get; init; }

    /// <summary>The name of the existing ground surface.</summary>
    public string ExistingSurfaceName { get; init; } = string.Empty;

    /// <summary>The id of the proposed surface.</summary>
    public long ProposedSurfaceId { get; init; }

    /// <summary>The name of the proposed surface.</summary>
    public string ProposedSurfaceName { get; init; } = string.Empty;

    /// <summary>Whether volumes were computed.</summary>
    public CutFillStatus Status { get; init; }

    /// <summary>Why volumes could not be computed; null when computed.</summary>
    public string? NotSupportedReason { get; init; }

    /// <summary>The cut (excavation) volume in cubic drawing units.</summary>
    public double CutVolume { get; init; }

    /// <summary>The fill (embankment) volume in cubic drawing units.</summary>
    public double FillVolume { get; init; }

    /// <summary>The signed net volume (cut − fill); positive = net cut.</summary>
    public double NetVolume { get; init; }

    /// <summary>The surface area used for the calculation, in square drawing units.</summary>
    public double SurfaceAreaUsed { get; init; }

    /// <summary>An overall verdict, for example <c>Predominantly Cut</c> or <c>Not Supported</c>.</summary>
    public string Verdict { get; init; } = string.Empty;

    /// <summary>True when the net volume is within the balance threshold of the total volume.</summary>
    public bool IsBalanced { get; init; }
}
