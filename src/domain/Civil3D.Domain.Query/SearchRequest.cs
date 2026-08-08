namespace Civil3D.Domain.Query;

/// <summary>
/// The request shape of <c>search_objects</c>. The free-text <see cref="Query"/> is matched
/// case-insensitively against the searchable text fields (name, description) of the selected
/// entity kinds. An empty <see cref="Kinds"/> searches every kind.
/// </summary>
public sealed record SearchRequest
{
    /// <summary>The free-text search term.</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>The entity kinds to search (for example <c>alignment</c>, <c>surface</c>), or null for all.</summary>
    public IReadOnlyList<string>? Kinds { get; init; }

    /// <summary>The pagination parameters.</summary>
    public PageRequest? Page { get; init; }
}
