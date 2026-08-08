namespace Civil3D.Tools.Surface.Dtos;

/// <summary>
/// A single difference between the two surfaces for one metric, with its severity and a
/// human-readable description.
/// </summary>
public sealed record SurfaceDifference
{
    /// <summary>The metric that differs, for example <c>maxElevation</c>.</summary>
    public string MetricKey { get; init; } = string.Empty;

    /// <summary>The metric name, for example <c>Maximum elevation</c>.</summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>A description of the difference, for example <c>Maximum elevation is 3.2 higher.</c></summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the difference is.</summary>
    public ComparisonSeverity Severity { get; init; }
}
