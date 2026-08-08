namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// Input for <c>surface_comparison_report</c>: the two surfaces to compare, identified by their
/// stable numeric ids. Both optional toggles default to <see langword="true"/> so the full
/// report is produced unless the caller opts out.
/// </summary>
public sealed record SurfaceComparisonRequest
{
    /// <summary>The id of the existing (reference) surface.</summary>
    public long ExistingSurfaceId { get; init; }

    /// <summary>The id of the proposed (candidate) surface.</summary>
    public long ProposedSurfaceId { get; init; }

    /// <summary>When true (default), the report includes the numeric statistics section.</summary>
    public bool IncludeStatistics { get; init; } = true;

    /// <summary>When true (default), the report includes the recommendation section.</summary>
    public bool IncludeRecommendations { get; init; } = true;
}
