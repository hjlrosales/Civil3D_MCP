using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Update-pipe orchestration: invokes the write repository inside the active transaction and
/// raises the <c>PartUpdated</c> domain event. Autodesk-free.
/// </summary>
public interface IUpdatePipeService
{
    /// <summary>Applies the changes described by <paramref name="specification"/> inside the active write transaction.</summary>
    /// <param name="transaction">The active write transaction (document-locked).</param>
    /// <param name="specification">The changes to apply.</param>
    /// <param name="context">Per-command execution context.</param>
    UpdatePipeResult Update(IWriteTransaction transaction, UpdatePipeSpecification specification, ICommandExecutionContext context);
}
