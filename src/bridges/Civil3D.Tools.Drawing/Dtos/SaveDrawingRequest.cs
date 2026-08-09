namespace Civil3D.Tools.Drawing.Dtos;

/// <summary>
/// Input of <c>save_drawing</c>: whether the current view should be zoomed to the drawing extents
/// after the save. Defaults to true so geometry created just before the save is visible.
/// </summary>
public sealed record SaveDrawingRequest
{
    /// <summary>When true (default), zoom the current view to the drawing extents after saving.</summary>
    public bool ZoomExtents { get; init; } = true;
}
