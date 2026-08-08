namespace Civil3D.Domain.Query;

/// <summary>
/// A single sort key. Multiple sort expressions are applied in order (first expression is the
/// primary key). <see cref="Field"/> is the case-insensitive property name on the queried DTO.
/// </summary>
public sealed record SortExpression
{
    /// <summary>The property name to sort by (case-insensitive).</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>The sort direction.</summary>
    public SortDirection Direction { get; init; } = SortDirection.Ascending;
}
