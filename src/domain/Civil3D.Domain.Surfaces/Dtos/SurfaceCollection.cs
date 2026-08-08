using System.Text.Json.Serialization;

namespace Civil3D.Domain.Surfaces.Dtos;

/// <summary>
/// Immutable collection of <see cref="SurfaceInfo"/> returned by surface repositories.
/// </summary>
public sealed record SurfaceCollection(IReadOnlyList<SurfaceInfo> Items)
{
    /// <summary>The number of surfaces in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no surfaces.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
