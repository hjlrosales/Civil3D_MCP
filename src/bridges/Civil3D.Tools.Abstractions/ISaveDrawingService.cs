namespace Civil3D.Tools.Abstractions;

/// <summary>
/// Persists the active drawing to its current path and optionally refreshes the current view to
/// the drawing extents so newly created geometry (for example pipes) is visible. The bridge
/// registers a real implementation that saves the active database and zooms on the application
/// context; tests substitute an in-memory fake.
/// </summary>
public interface ISaveDrawingService
{
    /// <summary>
    /// Saves the active drawing in place and, when <paramref name="zoomToExtents"/> is true, zooms
    /// the current view to the drawing extents. Implementations must map any failure to a
    /// <c>BridgeException</c> (for example <c>E_NO_ACTIVE_DOCUMENT</c> when no drawing is open or
    /// <c>E_TRANSACTION_FAILED</c> when the drawing is read-only or has never been saved).
    /// </summary>
    /// <param name="drawing">The validated active drawing snapshot.</param>
    /// <param name="zoomToExtents">When true, zoom to extents after saving.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    SaveDrawingResult Save(ActiveDrawing drawing, bool zoomToExtents, CancellationToken cancellationToken);
}
