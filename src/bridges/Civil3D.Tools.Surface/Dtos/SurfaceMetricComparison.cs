namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// One compared metric between the two surfaces. Values are rendered as strings so both
/// numeric and textual metrics (names, surface kinds) share one shape.
/// </summary>
public sealed record SurfaceMetricComparison
{
    /// <summary>A stable machine-readable metric key, for example <c>pointCount</c>.</summary>
    public string MetricKey { get; init; } = string.Empty;

    /// <summary>A human-readable metric name, for example <c>Point count</c>.</summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>The existing surface's value, rendered for display.</summary>
    public string ExistingValue { get; init; } = string.Empty;

    /// <summary>The proposed surface's value, rendered for display.</summary>
    public string ProposedValue { get; init; } = string.Empty;

    /// <summary>The unit of the metric, for example <c>points</c> or <c>elevation</c>; empty for unitless metrics.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>True when the metric differs in a way that matters (see the comparison options).</summary>
    public bool IsSignificant { get; init; }
}
