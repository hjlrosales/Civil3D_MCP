using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Write seam for updating an existing pipe. The pipeline hands the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked); the Autodesk
/// implementation opens the pipe by its stable numeric id and applies the requested geometry and
/// size changes. Throws <see cref="Civil3D.Domain.Errors.DomainException"/> on failure (pipe not
/// found, Civil 3D rejected the change, transaction failure).
/// </summary>
public interface IPipeUpdateRepository
{
    /// <summary>Applies the requested changes to the pipe with the given id.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="specification">The changes to apply.</param>
    UpdatePipeOutcome Update(IWriteTransaction transaction, UpdatePipeSpecification specification);
}
