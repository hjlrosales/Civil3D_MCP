namespace Civil3D.Tools.Project.Analysis;

/// <summary>
/// Tunable thresholds for <see cref="ProjectAnalyzer"/>: the complexity score bands, the
/// large-drawing entity threshold and the inventory name-list cap. Defaults are conservative;
/// future versions may surface them through the tool input.
/// </summary>
public sealed record ProjectSummaryOptions
{
    /// <summary>The default options.</summary>
    public static ProjectSummaryOptions Default { get; } = new();

    /// <summary>Score below which a drawing is classified as Small (default 10).</summary>
    public double SmallScoreThreshold { get; init; } = 10;

    /// <summary>Score below which a drawing is classified as Medium (default 25).</summary>
    public double MediumScoreThreshold { get; init; } = 25;

    /// <summary>Score below which a drawing is classified as Large (default 50); at or above is Enterprise.</summary>
    public double LargeScoreThreshold { get; init; } = 50;

    /// <summary>Maximum number of names kept per inventory list (default 100).</summary>
    public int MaxNameListLength { get; init; } = 100;

    /// <summary>Total entity count at or above which the drawing is flagged as large (default 100,000).</summary>
    public int LargeDrawingEntityThreshold { get; init; } = 100_000;
}
