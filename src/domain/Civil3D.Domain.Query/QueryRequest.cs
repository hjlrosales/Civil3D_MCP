namespace Civil3D.Domain.Query;

/// <summary>
/// The reusable request shape of every read-only query. All members are optional: an empty
/// request returns the first <see cref="PageRequest.DefaultPageSize"/> items in document order.
/// Filters are AND-ed together; sorts are applied in order; paging is applied last.
/// </summary>
public sealed record QueryRequest
{
    /// <summary>The filters to apply (AND semantics).</summary>
    public IReadOnlyList<FilterExpression>? Filters { get; init; }

    /// <summary>The sort keys, first is primary.</summary>
    public IReadOnlyList<SortExpression>? Sorts { get; init; }

    /// <summary>The pagination parameters.</summary>
    public PageRequest? Page { get; init; }

    /// <summary>The optional field selection (validated, see <see cref="FieldSelection"/>).</summary>
    public FieldSelection? Fields { get; init; }
}
