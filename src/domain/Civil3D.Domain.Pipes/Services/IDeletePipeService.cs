using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Delete-pipe orchestration: invokes the write repository inside the active transaction and
/// raises the <c>PartDeleted</c> domain event. Autodesk-free.
/// </summary>
public interface IDeletePipeService
{
    /// <summary>Deletes the pipe described by <paramref name="specification"/> inside the active write transaction.</summary>
    /// <param name="transaction">The active write transaction (document-locked).</param>
    /// <param name="specification">The pipe to delete.</param>
    /// <param name="context">Per-command execution context.</param>
    DeletePipeResult Delete(IWriteTransaction transaction, DeletePipeSpecification specification, ICommandExecutionContext context);
}
