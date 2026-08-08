namespace Civil3D.Domain.Query;

/// <summary>
/// Optional projection request. Empty selection means every field is returned. Results are
/// strongly typed immutable DTOs, so the engine validates that every requested field exists on
/// the target type (fail fast on typos) but does not drop fields from the returned records.
/// </summary>
public sealed record FieldSelection
{
    /// <summary>The requested field names (case-insensitive), or null for all fields.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>True when no fields were requested (return everything).</summary>
    public bool IsEmpty => Fields is null || Fields.Count == 0;
}
