using System.Text.Json.Serialization;

namespace Civil3D.Domain.Cogo.Dtos;

/// <summary>
/// Immutable collection of <see cref="CogoPointInfo"/> returned by COGO repositories.
/// </summary>
public sealed record CogoPointCollection(IReadOnlyList<CogoPointInfo> Items)
{
    /// <summary>The number of points in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no points.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
