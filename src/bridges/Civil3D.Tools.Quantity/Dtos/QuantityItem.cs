namespace Civil3D.Tools.Quantity.Dtos;

/// <summary>
/// One quantity line item: a named measurement within a category. Items are produced by the
/// pure calculation engine over the materialized domain data; no Autodesk types are exposed.
/// </summary>
public sealed record QuantityItem
{
    /// <summary>The discipline this item belongs to.</summary>
    public QuantityCategory Category { get; init; }

    /// <summary>A stable machine-readable item key, for example <c>alignment.total_length</c>.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>A human-readable label, for example <c>Total alignment length</c>.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The measured value.</summary>
    public double Quantity { get; init; }

    /// <summary>The unit of measure.</summary>
    public QuantityUnit Unit { get; init; } = QuantityUnit.Count;

    /// <summary>Optional detail, for example a breakdown of how the quantity was derived.</summary>
    public string? Detail { get; init; }
}
