namespace Civil3D.Tools.CutFill.Analysis;

/// <summary>
/// Thresholds the cut/fill analyzer uses to classify the earthwork result and decide which
/// recommendations to emit. All values are ratios; defaults apply when the caller supplies none.
/// </summary>
public sealed record CutFillOptions
{
    /// <summary>The default threshold set.</summary>
    public static CutFillOptions Default { get; } = new();

    /// <summary>
    /// When |net| ÷ (cut + fill) is at or below this ratio the result is classified as balanced
    /// earthwork and a <c>Balanced earthwork</c> recommendation is produced.
    /// </summary>
    public double BalanceThreshold { get; init; } = 0.10;

    /// <summary>
    /// When |net| ÷ (cut + fill) is at or above this ratio a <c>Significant net export</c>
    /// (net cut) or <c>Significant net import</c> (net fill) recommendation is produced.
    /// </summary>
    public double SignificantImbalanceRatio { get; init; } = 0.25;

    /// <summary>
    /// When the surface point-count delta is at least this ratio of the larger count, a
    /// <c>Verify surface quality before construction</c> recommendation is produced.
    /// </summary>
    public double SurfaceQualityPointRatio { get; init; } = 0.25;
}
