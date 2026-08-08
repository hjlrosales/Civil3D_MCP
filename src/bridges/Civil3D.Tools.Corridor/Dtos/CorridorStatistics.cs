namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// Aggregate statistics over the analyzed corridor set. Produced only when statistics are
/// enabled; all values derive from the exposed <see cref="CorridorSummary"/> metrics.
/// </summary>
public sealed record CorridorStatistics
{
    /// <summary>The number of corridors analyzed.</summary>
    public int CorridorCount { get; init; }

    /// <summary>The total number of baselines across all corridors.</summary>
    public int TotalBaselineCount { get; init; }

    /// <summary>The total number of corridor surfaces across all corridors.</summary>
    public int TotalCorridorSurfaceCount { get; init; }

    /// <summary>The number of corridors with at least one baseline.</summary>
    public int CorridorsWithBaselines { get; init; }

    /// <summary>The number of corridors without any baseline.</summary>
    public int CorridorsWithoutBaselines { get; init; }

    /// <summary>The number of corridors with at least one corridor surface.</summary>
    public int CorridorsWithSurfaces { get; init; }

    /// <summary>The number of corridors without any corridor surface.</summary>
    public int CorridorsWithoutSurfaces { get; init; }

    /// <summary>Average baselines per corridor; 0 when there are no corridors.</summary>
    public double AverageBaselinesPerCorridor { get; init; }
}
