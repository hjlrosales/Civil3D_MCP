using Civil3D.Tools.Quantity.Dtos;

namespace Civil3D.Tools.Quantity.Analysis;

/// <summary>
/// The calculation-engine output: the drawing overview, the quantity line items, the per-category
/// roll-ups and the aggregate statistics. Immutable; produced by
/// <see cref="QuantityCalculator.Calculate"/>.
/// </summary>
public sealed record QuantityTakeoffResult(
    QuantityOverview Overview,
    IReadOnlyList<QuantityItem> Items,
    IReadOnlyList<QuantitySummary> Summaries,
    QuantityStatistics Statistics);
