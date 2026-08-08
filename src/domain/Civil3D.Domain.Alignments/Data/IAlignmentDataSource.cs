using Civil3D.Domain.Alignments.Dtos;

namespace Civil3D.Domain.Alignments.Data;

/// <summary>
/// The Autodesk seam for the alignments discipline. Reads every alignment once inside a single
/// read-only transaction and returns immutable DTOs; never exposes Autodesk objects. Implemented
/// by <c>AutodeskAlignmentDataSource</c> in production and by an in-memory fake in tests.
/// </summary>
public interface IAlignmentDataSource
{
    /// <summary>
    /// Reads all alignments in the active drawing exactly once and materializes them.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    AlignmentCollection ReadAll(CancellationToken cancellationToken = default);
}
