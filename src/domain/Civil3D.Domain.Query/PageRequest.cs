namespace Civil3D.Domain.Query;

/// <summary>
/// Pagination parameters. Pages are 1-based; page size is clamped to 1..<see cref="MaxPageSize"/>.
/// </summary>
public sealed record PageRequest
{
    /// <summary>The maximum allowed page size (defense against huge result sets).</summary>
    public const int MaxPageSize = 500;

    /// <summary>The default page size when none is specified.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>The 1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; } = DefaultPageSize;
}
