using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Write seam for creating pipes. The pipeline hands the active <see cref="IWriteTransaction"/>
/// (already begun and document-locked); the Autodesk implementation opens the target network,
/// resolves the pipe part family and size from its parts list, and adds the pipe. Throws
/// <see cref="Civil3D.Domain.Errors.DomainException"/> on failure (network not found, no or
/// ambiguous part family match, transaction failure).
/// </summary>
public interface IPipeCreateRepository
{
    /// <summary>Creates a pipe in the network with the given id.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="networkId">Stable numeric id of the target network.</param>
    /// <param name="specification">The pipe to create.</param>
    CreatePipeOutcome Create(IWriteTransaction transaction, long networkId, CreatePipeSpecification specification);
}
