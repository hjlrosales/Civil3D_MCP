using Civil3D.Domain.Surfaces.Dtos;

namespace Civil3D.Domain.Surfaces.Data;

/// <summary>
/// The Autodesk seam for the surfaces discipline. Reads every surface once inside a single
/// read-only transaction and returns immutable DTOs; never exposes Autodesk objects.
/// </summary>
public interface ISurfaceDataSource
{
    /// <summary>Reads all surfaces in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    SurfaceCollection ReadAll(CancellationToken cancellationToken = default);
}
