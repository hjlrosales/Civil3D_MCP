using Civil3D.Domain.Corridors.Dtos;

namespace Civil3D.Domain.Corridors.Data;

/// <summary>
/// The Autodesk seam for the corridors discipline. Reads every corridor once inside a single
/// read-only transaction and returns immutable DTOs; never exposes Autodesk objects.
/// </summary>
public interface ICorridorDataSource
{
    /// <summary>Reads all corridors in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    CorridorCollection ReadAll(CancellationToken cancellationToken = default);
}
