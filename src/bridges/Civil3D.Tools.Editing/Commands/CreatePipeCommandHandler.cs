using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;

namespace Civil3D.Tools.Editing.Commands;

/// <summary>
/// Handler for <see cref="CreatePipeCommand"/>. Orchestrates the write transaction lifecycle:
/// opens an undo unit, delegates to <see cref="ICreatePipeService"/> (which resolves the part and
/// performs the Autodesk creation inside the active write transaction), and commits the undo unit
/// on success or rolls it back on failure. Autodesk-free.
/// </summary>
public sealed class CreatePipeCommandHandler : ICommandHandler<CreatePipeCommand, CreatePipeResult>
{
    private readonly ICreatePipeService _service;

    /// <summary>Creates the handler bound to the create-pipe domain service.</summary>
    /// <param name="service">The create-pipe domain service.</param>
    public CreatePipeCommandHandler(ICreatePipeService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
    public CreatePipeResult Handle(
        CreatePipeCommand command,
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

        using IUndoUnit undo = context.Undo.Begin($"Create pipe in {command.NetworkName}");
        try
        {
            var specification = new CreatePipeSpecification
            {
                NetworkName = command.NetworkName,
                PartFamilyMatch = command.PartFamilyMatch,
                FallbackMatch = string.IsNullOrWhiteSpace(command.Material) ? null : command.Material.Trim(),
                Material = string.IsNullOrWhiteSpace(command.Material) ? null : command.Material.Trim(),
                Sdr = string.IsNullOrWhiteSpace(command.Sdr) ? null : command.Sdr.Trim(),
                PressureClassBar = command.PressureClassBar,
                DiameterMm = command.DiameterMm,
                StartEasting = command.StartEasting,
                StartNorthing = command.StartNorthing,
                StartElevation = command.StartElevation,
                EndEasting = command.EndEasting,
                EndNorthing = command.EndNorthing,
                EndElevation = command.StartElevation,
                Description = command.Description,
            };

            CreatePipeResult result = _service.Create(transaction, specification, context);
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
