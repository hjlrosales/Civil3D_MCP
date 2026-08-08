namespace Civil3D.Tools.Abstractions;

/// <summary>
/// Read-only view over the host application session (Civil 3D). The bridge registers a real
/// implementation that reads <c>Application.DocumentManager.MdiActiveDocument</c>; tests substitute
/// an in-memory fake. All document availability checks flow through this contract so tool code and
/// unit tests never touch the Autodesk API directly.
/// </summary>
public interface ICivil3DSession
{
    /// <summary>
    /// Returns a snapshot of the active drawing, or <see langword="null"/> when no drawing is
    /// open. Must be invoked on the application context (all Civil 3D tools run there via
    /// <see cref="Civil3DToolBase{TIn,TOut}.RequiresApplicationContext"/>).
    /// </summary>
    ActiveDrawing? GetActiveDrawing();
}
