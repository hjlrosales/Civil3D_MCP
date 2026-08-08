namespace Civil3D.Tools.CutFill.Dtos;

/// <summary>
/// A per-metric difference between the two surfaces that contextualises the volume result
/// (point count, elevation range, mean elevation). Derived from the loaded domain DTOs only.
/// </summary>
public sealed record VolumeDifference
{
    /// <summary>A stable machine-readable metric key, for example <c>pointCount</c>.</summary>
    public string MetricKey { get; init; } = string.Empty;

    /// <summary>A human-readable metric name, for example <c>Point count</c>.</summary>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>The existing surface's value, rendered for display.</summary>
    public string ExistingValue { get; init; } = string.Empty;

    /// <summary>The proposed surface's value, rendered for display.</summary>
    public string ProposedValue { get; init; } = string.Empty;

    /// <summary>A description of the difference.</summary>
    public string Description { get; init; } = string.Empty;
}
