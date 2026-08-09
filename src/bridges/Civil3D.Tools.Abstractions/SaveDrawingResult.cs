namespace Civil3D.Tools.Abstractions;

/// <summary>
/// The immutable result of <c>save_drawing</c>: where the drawing was saved, when, and whether the
/// view was zoomed to the drawing extents. Serialized to the wire by the dispatcher.
/// </summary>
public sealed record SaveDrawingResult
{
    /// <summary>True when the drawing was saved to disk.</summary>
    public bool Success { get; init; }

    /// <summary>File name of the saved drawing.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>Full path the drawing was saved to.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>UTC timestamp of the save.</summary>
    public DateTime SavedAtUtc { get; init; }

    /// <summary>Whether the current view was zoomed to the drawing extents.</summary>
    public bool ZoomedToExtents { get; init; }
}
