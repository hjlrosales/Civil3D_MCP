namespace Civil3D.Domain.Query;

/// <summary>
/// A single page of search results plus paging metadata. Immutable; serialized directly as the
/// tool output.
/// </summary>
/// <typeparam name="T">The result DTO type (for example <c>ObjectReference</c>).</typeparam>
/// <param name="Items">The results on this page.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The page size used.</param>
/// <param name="TotalCount">The total number of matches before paging.</param>
public sealed record SearchResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>The total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    /// <summary>True when a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>True when a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Execution statistics for the search.</summary>
    public QueryStatistics Statistics { get; init; } = new(TotalCount, Items.Count, 0);
}
