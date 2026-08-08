namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// Per-category roll-up of the quantity items: the number of items measured in the category and
/// the sum of their quantities. Only <c>Count</c>-unit items are summed into
/// <see cref="TotalQuantity"/>; measured lengths and sizes are reported in their own items so the
/// aggregate stays dimensionally meaningful.
/// </summary>
public sealed record QuantitySummary
{
    /// <summary>The category being summarized.</summary>
    public QuantityCategory Category { get; init; }

    /// <summary>The number of line items in the category.</summary>
    public int ItemCount { get; init; }

    /// <summary>The sum of count-unit quantities in the category.</summary>
    public double TotalQuantity { get; init; }

    /// <summary>A human-readable total label, for example <c>2 objects</c>.</summary>
    public string TotalLabel { get; init; } = string.Empty;
}
