namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// Optional numeric statistics of the comparison (proposed minus existing for every delta).
/// Produced only when <c>IncludeStatistics</c> is set on the request.
/// </summary>
public sealed record SurfaceComparisonStatistics
{
    /// <summary>The difference in surface definition points (proposed − existing).</summary>
    public int PointCountDelta { get; init; }

    /// <summary>The relative point-count change as a percentage of the larger count; 0 when both are empty.</summary>
    public double PointCountDeltaPercent { get; init; }

    /// <summary>The difference in minimum elevation.</summary>
    public double MinElevationDelta { get; init; }

    /// <summary>The difference in maximum elevation.</summary>
    public double MaxElevationDelta { get; init; }

    /// <summary>The difference in mean (average) elevation.</summary>
    public double MeanElevationDelta { get; init; }

    /// <summary>The difference in elevation range (max − min).</summary>
    public double ElevationRangeDelta { get; init; }
}
