namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// The headline of the surface comparison: the identities of both surfaces, the number of
/// metrics/differences/recommendations and an overall verdict.
/// </summary>
public sealed record SurfaceComparisonSummary
{
    /// <summary>The id of the existing (reference) surface.</summary>
    public long ExistingSurfaceId { get; init; }

    /// <summary>The name of the existing surface.</summary>
    public string ExistingSurfaceName { get; init; } = string.Empty;

    /// <summary>The id of the proposed (candidate) surface.</summary>
    public long ProposedSurfaceId { get; init; }

    /// <summary>The name of the proposed surface.</summary>
    public string ProposedSurfaceName { get; init; } = string.Empty;

    /// <summary>The number of metrics compared.</summary>
    public int MetricCount { get; init; }

    /// <summary>The number of metrics that differ.</summary>
    public int DifferenceCount { get; init; }

    /// <summary>The number of differences flagged as significant.</summary>
    public int SignificantDifferenceCount { get; init; }

    /// <summary>The number of recommendations produced.</summary>
    public int RecommendationCount { get; init; }

    /// <summary>An overall verdict: <c>Compatible</c> or <c>Review Required</c>.</summary>
    public string Verdict { get; init; } = string.Empty;
}
