using System.Text.Json.Serialization;

namespace Civil3D.Domain.Alignments.Dtos;

/// <summary>
/// Immutable collection of <see cref="AlignmentInfo"/> returned by alignment repositories.
/// Wraps a materialized list; no lazy Autodesk handles survive the read.
/// </summary>
public sealed record AlignmentCollection(IReadOnlyList<AlignmentInfo> Items)
{
    /// <summary>The number of alignments in the collection.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>True when the collection contains no alignments.</summary>
    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0;
}
