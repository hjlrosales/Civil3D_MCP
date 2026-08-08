namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// Input for <c>calculate_cut_fill</c>: the two surfaces to compare, identified by their stable
/// numeric ids. Both optional toggles default to <see langword="true"/> so the full report is
/// produced unless the caller opts out.
/// </summary>
public sealed record CutFillRequest
{
    /// <summary>The id of the existing ground (reference) surface.</summary>
    public long ExistingSurfaceId { get; init; }

    /// <summary>The id of the proposed (design) surface.</summary>
    public long ProposedSurfaceId { get; init; }

    /// <summary>When true (default), the report includes the numeric statistics section.</summary>
    public bool IncludeStatistics { get; init; } = true;

    /// <summary>When true (default), the report includes the recommendation section.</summary>
    public bool IncludeRecommendations { get; init; } = true;
}
