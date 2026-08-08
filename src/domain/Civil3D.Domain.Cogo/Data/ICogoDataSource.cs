using Civil3D.Domain.Cogo.Dtos;

namespace Civil3D.Domain.Cogo.Data;

/// <summary>
/// The Autodesk seam for the COGO discipline. Reads every point once inside a single read-only
/// transaction and returns immutable DTOs; never exposes Autodesk objects.
/// </summary>
public interface ICogoDataSource
{
    /// <summary>Reads all COGO points in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    CogoPointCollection ReadAll(CancellationToken cancellationToken = default);
}
