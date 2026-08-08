namespace Civil3D.Tools.Abstractions;

/// <summary>
/// Computes the lightweight statistics of the active drawing (symbol table counts, space entity
/// counts, approximate file size). The bridge registers a real implementation that runs a single
/// read-only transaction over the active database; tests substitute an in-memory fake.
/// </summary>
public interface IDrawingStatisticsService
{
    /// <summary>
    /// Computes the drawing statistics. Implementations must open, read, commit and dispose a
    /// read-only transaction — never edit the drawing — and must map any failure to a
    /// <c>BridgeException</c> (for example <c>E_TRANSACTION_FAILED</c>).
    /// </summary>
    /// <param name="drawing">The validated active drawing snapshot.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    DrawingStatistics GetStatistics(ActiveDrawing drawing, CancellationToken cancellationToken);
}
