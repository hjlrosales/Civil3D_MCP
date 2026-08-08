namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// Input for <c>corridor_analysis_report</c>: analyze one corridor by id, or all corridors when
/// <c>CorridorId</c> is omitted. Both optional toggles default to <see langword="true"/> so the
/// full report is produced unless the caller opts out.
/// </summary>
public sealed record CorridorAnalysisRequest
{
    /// <summary>The corridor to analyze; null (default) analyzes every corridor.</summary>
    public long? CorridorId { get; init; }

    /// <summary>When true (default), the report includes the aggregate statistics section.</summary>
    public bool IncludeStatistics { get; init; } = true;

    /// <summary>When true (default), the report includes the recommendation section.</summary>
    public bool IncludeRecommendations { get; init; } = true;
}
