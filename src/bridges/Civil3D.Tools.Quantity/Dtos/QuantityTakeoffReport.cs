namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// The <c>quantity_takeoff_report</c> result: a structured, read-only quantity summary of the
/// active Civil 3D drawing. Combines drawing identity, the per-item quantity lines, per-category
/// roll-ups, aggregate statistics and execution accounting. Immutable and Autodesk-free.
/// </summary>
public sealed record QuantityTakeoffReport
{
    /// <summary>The drawing identity section.</summary>
    public QuantityOverview Overview { get; init; } = new();

    /// <summary>The quantity line items, ordered by category then key.</summary>
    public IReadOnlyList<QuantityItem> Items { get; init; } = Array.Empty<QuantityItem>();

    /// <summary>Per-category roll-ups of the line items.</summary>
    public IReadOnlyList<QuantitySummary> Summaries { get; init; } = Array.Empty<QuantitySummary>();

    /// <summary>Aggregate statistics across all disciplines.</summary>
    public QuantityStatistics Statistics { get; init; } = new();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
