using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Write seam for creating pipe networks. The pipeline hands the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked); the Autodesk
/// implementation creates the parts list (adding the requested material families from the
/// installed pipe catalog) and the network, then assigns the parts list to the network. Throws
/// <see cref="Civil3D.Domain.Errors.DomainException"/> on failure.
/// </summary>
public interface IPipeNetworkCreateRepository
{
    /// <summary>Creates the pipe network described by <paramref name="specification"/>.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="specification">The network to create.</param>
    CreatePipeNetworkOutcome Create(IWriteTransaction transaction, CreatePipeNetworkSpecification specification);
}
