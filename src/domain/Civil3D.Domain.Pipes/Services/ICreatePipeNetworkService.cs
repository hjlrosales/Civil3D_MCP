using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;

namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Create-pipe-network orchestration: validates the name is free, invokes the write repository
/// inside the active transaction, and raises the <c>NetworkCreated</c> domain event. Autodesk-free.
/// </summary>
public interface ICreatePipeNetworkService
{
    /// <summary>Creates the pipe network described by <paramref name="specification"/> inside the active write transaction.</summary>
    /// <param name="transaction">The active write transaction (document-locked).</param>
    /// <param name="specification">The network to create.</param>
    /// <param name="context">Per-command execution context.</param>
    CreatePipeNetworkResult Create(IWriteTransaction transaction, CreatePipeNetworkSpecification specification, ICommandExecutionContext context);
}
