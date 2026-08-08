namespace Civil3D.Tools.Export.Dtos;

/// <summary>
/// An object the exporter could not write (for example an object type the installed API does
/// not support) plus the reason it was skipped.
/// </summary>
public sealed record SkippedObject
{
    /// <summary>The object type, for example <c>Corridor</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The object name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The object id, when available.</summary>
    public long Id { get; init; }

    /// <summary>Why the object was skipped.</summary>
    public string Reason { get; init; } = string.Empty;
}
