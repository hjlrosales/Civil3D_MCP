namespace Civil3D.Tools.Health.Analysis;

/// <summary>
/// Tunable thresholds for <see cref="HealthAnalyzer"/>. All values have conservative defaults so
/// the analyzer behaves predictably out of the box; future versions may surface these through the
/// tool input.
/// </summary>
public sealed record HealthAnalyzerOptions
{
    /// <summary>The default options.</summary>
    public static HealthAnalyzerOptions Default { get; } = new();

    /// <summary>Total entity count at or above which the drawing is flagged as large (default 100,000).</summary>
    public int LargeDrawingEntityThreshold { get; init; } = 100_000;

    /// <summary>Model space entity count at or above which model space is flagged as dense (default 50,000).</summary>
    public int LargeModelSpaceEntityThreshold { get; init; } = 50_000;

    /// <summary>Surface point count at or above which the surface is flagged as large (default 500,000).</summary>
    public int LargeSurfacePointThreshold { get; init; } = 500_000;

    /// <summary>COGO point count at or above which the point collection is flagged as large (default 10,000).</summary>
    public int LargeCogoPointThreshold { get; init; } = 10_000;
}
