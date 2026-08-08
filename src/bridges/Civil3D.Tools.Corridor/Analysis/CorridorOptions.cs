namespace Civil3D.Tools.Corridor.Analysis;

/// <summary>
/// Thresholds the corridor analyzer uses to decide health verdicts and which recommendations to
/// emit. Defaults apply when the caller supplies none.
/// </summary>
public sealed record CorridorOptions
{
    /// <summary>The default threshold set.</summary>
    public static CorridorOptions Default { get; } = new();

    /// <summary>A corridor with at least this many baselines is flagged as highly complex.</summary>
    public int LargeComplexityBaselineThreshold { get; init; } = 4;

    /// <summary>A corridor with at least this many corridor surfaces is flagged as highly complex.</summary>
    public int LargeComplexitySurfaceThreshold { get; init; } = 3;
}
