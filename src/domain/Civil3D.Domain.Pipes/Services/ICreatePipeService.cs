using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Create-pipe orchestration: validates the target network exists, invokes the write repository
/// inside the active transaction, and raises the <c>PartCreated</c> domain event. Autodesk-free.
/// </summary>
public interface ICreatePipeService
{
    /// <summary>Creates the pipe described by <paramref name="specification"/> inside the active write transaction.</summary>
    /// <param name="transaction">The active write transaction (document-locked).</param>
    /// <param name="specification">The pipe to create.</param>
    /// <param name="context">Per-command execution context.</param>
    CreatePipeResult Create(IWriteTransaction transaction, CreatePipeSpecification specification, ICommandExecutionContext context);
}
