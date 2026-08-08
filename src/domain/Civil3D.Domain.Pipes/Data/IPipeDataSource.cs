using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Data;

/// <summary>
/// The Autodesk seam for the pipe-networks discipline. Reads every network (and its parts) once
/// inside a single read-only transaction and returns immutable DTOs; never exposes Autodesk objects.
/// </summary>
public interface IPipeDataSource
{
    /// <summary>Reads all pipe networks in the active drawing exactly once and materializes them.</summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    PipeNetworkCollection ReadAll(CancellationToken cancellationToken = default);
}
