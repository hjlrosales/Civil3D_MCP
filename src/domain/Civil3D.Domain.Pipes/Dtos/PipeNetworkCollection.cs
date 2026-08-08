using System.Text.Json.Serialization;

namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Immutable collection of <see cref="PipeNetworkInfo"/> returned by pipe repositories.
/// </summary>
public sealed record PipeNetworkCollection(IReadOnlyList<PipeNetworkInfo> Items)
{
    /// <summary>The number of networks in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no networks.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
