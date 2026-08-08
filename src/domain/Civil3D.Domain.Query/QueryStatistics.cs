namespace Civil3D.Domain.Query;

/// <summary>
/// Cheap execution statistics attached to every page or search result.
/// </summary>
/// <param name="MatchedCount">Items that passed filtering (before paging).</param>
/// <param name="ReturnedCount">Items returned on this page.</param>
/// <param name="ExecutionTimeMs">Time spent applying the query, in milliseconds.</param>
public sealed record QueryStatistics(int MatchedCount, int ReturnedCount, long ExecutionTimeMs);
