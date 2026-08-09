using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Write seam for deleting an existing pipe. The pipeline hands the active
/// <see cref="IWriteTransaction"/> (already begun and document-locked); the Autodesk
/// implementation opens the pipe by its stable numeric id, reads its identity back, and erases
/// it. Throws <see cref="Civil3D.Domain.Errors.DomainException"/> on failure (pipe not found,
/// Civil 3D rejected the erasure, transaction failure).
/// </summary>
public interface IPipeDeleteRepository
{
    /// <summary>Deletes the pipe with the given id and returns its identity read back.</summary>
    /// <param name="transaction">The active write transaction.</param>
    /// <param name="specification">The pipe to delete.</param>
    DeletePipeOutcome Delete(IWriteTransaction transaction, DeletePipeSpecification specification);
}
