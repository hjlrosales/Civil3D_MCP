using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Handler for <see cref="UpdatePipeCommand"/>. Orchestrates the write transaction lifecycle:
/// opens an undo unit, delegates to <see cref="IUpdatePipeService"/> (which performs the
/// Autodesk update inside the active write transaction), and commits the undo unit on success or
/// rolls it back on failure. Autodesk-free.
/// </summary>
public sealed class UpdatePipeCommandHandler : ICommandHandler<UpdatePipeCommand, UpdatePipeResult>
{
    private readonly IUpdatePipeService _service;

    /// <summary>Creates the handler bound to the update-pipe domain service.</summary>
    /// <param name="service">The update-pipe domain service.</param>
    public UpdatePipeCommandHandler(IUpdatePipeService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public UpdatePipeResult Handle(
        UpdatePipeCommand command,
        ICommandExecutionContext context,
        IWriteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            throw new CommandException(
                CommandErrorCode.TransactionFailed,
                $"Command '{command.Name}' requires a write transaction.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using IUndoUnit undo = context.Undo.Begin($"Update pipe {command.PipeId}");
        try
        {
            var specification = new UpdatePipeSpecification
            {
                PipeId = command.PipeId,
                ElevationMeters = command.ElevationMeters,
                LengthMeters = command.LengthMeters,
                DiameterMm = command.DiameterMm,
            };

            UpdatePipeResult result = _service.Update(transaction, specification, context);
            undo.Commit();
            return result;
        }
        catch
        {
            undo.Rollback();
            throw;
        }
    }
}
