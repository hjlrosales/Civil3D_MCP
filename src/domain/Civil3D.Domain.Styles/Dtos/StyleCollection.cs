using System.Text.Json.Serialization;

namespace Civil3D.Domain.Styles.Dtos;

/// <summary>
/// Immutable collection of <see cref="StyleInfo"/> returned by style repositories.
/// </summary>
public sealed record StyleCollection(IReadOnlyList<StyleInfo> Items)
{
    /// <summary>The number of styles in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no styles.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
