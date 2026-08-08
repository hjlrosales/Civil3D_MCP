using System.Text.Json.Serialization;

namespace Civil3D.Domain.Profiles.Dtos;

/// <summary>
/// Immutable collection of <see cref="ProfileInfo"/> returned by profile repositories.
/// </summary>
public sealed record ProfileCollection(IReadOnlyList<ProfileInfo> Items)
{
    /// <summary>The number of profiles in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no profiles.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
