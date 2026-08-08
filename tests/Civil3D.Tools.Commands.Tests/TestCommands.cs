using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using static Civil3D.Tools.Commands.Tests.TestDoubles;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>
/// Test-only commands, handlers and validators that exercise the framework through the tool layer.
/// These are test doubles, not production editing commands (those arrive in Phase 5B).
/// </summary>
internal static class TestCommands
{
    internal sealed record RecordLogResult(string Label, bool HadTransaction);

    /// <summary>A writing command whose Label is validated before execution.</summary>
    internal sealed class RecordLogCommand : ICommand<RecordLogResult>
    {
        public string Name => "test.record_log";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
        public string? Label { get; init; }
    }

    internal sealed class RecordLogCommandHandler(FakeWriteRepository repository)
        : ICommandHandler<RecordLogCommand, RecordLogResult>
    {
        public RecordLogResult Handle(
            RecordLogCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            repository.Entries.Add(command.Label ?? string.Empty);
            return new RecordLogResult(command.Label ?? string.Empty, transaction is not null);
        }
    }

    internal sealed class LabelRequiredValidator : ICommandValidator<RecordLogCommand>
    {
        public ValidationResult Validate(RecordLogCommand command)
            => string.IsNullOrWhiteSpace(command.Label)
                ? ValidationResult.Invalid(new ValidationFailure("Label", "Label must not be empty."))
                : ValidationResult.Valid;
    }

    /// <summary>A dangerous writing command that requires explicit confirmation.</summary>
    internal sealed class DestructiveCommand : ICommand<RecordLogResult>
    {
        public string Name => "test.destructive";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => true;
        public ConfirmationDescriptor? Confirmation => new()
        {
            Title = "Delete layer",
            Message = "This operation deletes a layer from the drawing.",
            Risk = "High",
        };
        public string? Label { get; init; }
    }

    internal sealed class DestructiveCommandHandler(FakeWriteRepository repository)
        : ICommandHandler<DestructiveCommand, RecordLogResult>
    {
        public RecordLogResult Handle(
            DestructiveCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            repository.Entries.Add(command.Label ?? "destructive");
            return new RecordLogResult(command.Label ?? "destructive", transaction is not null);
        }
    }

    /// <summary>A writing command whose handler fails with a domain error (rollback path).</summary>
    internal sealed class FailingCommand : ICommand<RecordLogResult>
    {
        public string Name => "test.failing";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
    }

    internal sealed class FailingCommandHandler : ICommandHandler<FailingCommand, RecordLogResult>
    {
        public RecordLogResult Handle(
            FailingCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
            => throw new DomainException(DomainErrorCode.TransactionFailed, "simulated database failure");
    }
}
