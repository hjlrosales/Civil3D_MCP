namespace Civil3D.Domain.Query;

/// <summary>
/// A single filter applied to a query. The <see cref="Field"/> is the case-insensitive property
/// name on the queried DTO; the <see cref="Operator"/> selects the comparison; <see cref="Value"/>
/// and <see cref="Values"/> carry the operands (values deserialize as JSON primitives or
/// <c>JsonElement</c> and are normalized by the engine). Only the closed set of
/// <see cref="FilterOperator"/> values is supported.
/// </summary>
public sealed record FilterExpression
{
    /// <summary>The property name to compare (case-insensitive).</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>The comparison operator.</summary>
    public FilterOperator Operator { get; init; }

    /// <summary>The operand for single-value operators (Equals, comparisons, …).</summary>
    public object? Value { get; init; }

    /// <summary>The operands for <see cref="FilterOperator.In"/> and <see cref="FilterOperator.NotIn"/>.</summary>
    public IReadOnlyList<object?>? Values { get; init; }
}
