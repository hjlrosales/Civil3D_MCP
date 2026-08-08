using System.Text.Json.Serialization;

namespace Civil3D.Domain.Corridors.Dtos;

/// <summary>
/// Immutable collection of <see cref="CorridorInfo"/> returned by corridor repositories.
/// </summary>
public sealed record CorridorCollection(IReadOnlyList<CorridorInfo> Items)
{
    /// <summary>The number of corridors in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no corridors.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
