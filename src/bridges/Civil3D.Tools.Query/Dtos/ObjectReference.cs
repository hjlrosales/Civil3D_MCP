namespace Civil3D.Tools.Query.Dtos;

/// <summary>
/// A lightweight, typed search hit returned by <c>search_objects</c>. <see cref="Layer"/> is not
/// yet available from the domain DTOs and is always null; <see cref="StyleName"/> is resolved from
/// the drawing's style collection when the object kind carries a style reference.
/// </summary>
public sealed record ObjectReference
{
    /// <summary>The entity kind, for example <c>alignment</c> or <c>surface</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The stable numeric id of the object.</summary>
    public long Id { get; init; }

    /// <summary>The display name of the object.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The description, or null when absent.</summary>
    public string? Description { get; init; }

    /// <summary>The layer name, when known (currently always null).</summary>
    public string? Layer { get; init; }

    /// <summary>The resolved style name, when the object kind carries a style reference.</summary>
    public string? StyleName { get; init; }
}
