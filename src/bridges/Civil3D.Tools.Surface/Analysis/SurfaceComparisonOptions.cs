namespace Civil3D.Tools.Surface.Analysis;

/// <summary>
/// Thresholds used by the comparison engine to decide when a metric difference is significant
/// and which recommendations to emit. All values are in drawing units / counts; defaults apply
/// when the caller supplies none.
/// </summary>
public sealed record SurfaceComparisonOptions
{
    /// <summary>The default threshold set.</summary>
    public static SurfaceComparisonOptions Default { get; } = new();

    /// <summary>
    /// A point-count difference of at least this ratio of the larger count is flagged as
    /// significant and produces a <c>Large point-count difference</c> recommendation.
    /// </summary>
    public double PointCountDifferenceRatio { get; init; } = 0.25;

    /// <summary>An elevation-range (max − min) difference at or above this value (drawing units) is significant.</summary>
    public double ElevationRangeTolerance { get; init; } = 2.0;

    /// <summary>A mean-elevation difference at or above this value (drawing units) is significant.</summary>
    public double MeanElevationTolerance { get; init; } = 1.0;

    /// <summary>
    /// When the proposed point count is below this ratio of the existing count, the proposed
    /// surface is considered potentially outdated or low resolution.
    /// </summary>
    public double OutdatedSurfaceRatio { get; init; } = 0.5;
}
