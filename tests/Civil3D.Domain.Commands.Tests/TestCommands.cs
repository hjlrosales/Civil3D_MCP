using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Errors;
using static Civil3D.Domain.Commands.Tests.TestDoubles;

namespace Civil3D.Domain.Commands.Tests;

/// <summary>
/// Test-only commands, handlers and validators that exercise the framework. These are test
/// doubles, not production editing commands (those arrive in Phase 5B).
/// </summary>
internal static class TestCommands
{
    internal sealed record WriteCommandResult(string CommandName, bool HadTransaction);

    /// <summary>A writing command whose Value is validated before execution.</summary>
    internal sealed class RecordWriteCommand : ICommand<WriteCommandResult>
    {
        public string Name => "record.write";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
        public string? Value { get; init; }
    }

    internal sealed class RecordWriteCommandHandler(FakeWriteRepository repository)
        : ICommandHandler<RecordWriteCommand, WriteCommandResult>
    {
        public WriteCommandResult Handle(
            RecordWriteCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            repository.Writes.Add(command.Value ?? string.Empty);
            return new WriteCommandResult(command.Name, transaction is not null);
        }
    }

    internal sealed class ValueRequiredValidator : ICommandValidator<RecordWriteCommand>
    {
        public ValidationResult Validate(RecordWriteCommand command)
            => string.IsNullOrWhiteSpace(command.Value)
                ? ValidationResult.Invalid(new ValidationFailure("Value", "Value must not be empty."))
                : ValidationResult.Valid;
    }

    internal sealed class ValueMaxLengthValidator : ICommandValidator<RecordWriteCommand>
    {
        public ValidationResult Validate(RecordWriteCommand command)
            => command.Value is null or { Length: > 10 }
                ? ValidationResult.Invalid(new ValidationFailure("Value", "Value must be between 1 and 10 characters."))
                : ValidationResult.Valid;
    }

    internal sealed record ProbeResult(string CommandName, bool HadTransaction);

    /// <summary>A read-only command: the pipeline must not open a transaction.</summary>
    internal sealed class ReadOnlyProbeCommand : ICommand<ProbeResult>
    {
        public string Name => "probe.read";
        public CommandPermission RequiredPermission => CommandPermission.ReadOnly;
        public bool IsReadOnly => true;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
    }

    internal sealed class ReadOnlyProbeCommandHandler : ICommandHandler<ReadOnlyProbeCommand, ProbeResult>
    {
        public ProbeResult Handle(
            ReadOnlyProbeCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
            => new ProbeResult(command.Name, transaction is not null);
    }

    /// <summary>A dangerous writing command that requires explicit confirmation.</summary>
    internal sealed class ConfirmationRequiredCommand : ICommand<WriteCommandResult>
    {
        public string Name => "confirm.write";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => true;
        public ConfirmationDescriptor? Confirmation => new()
        {
            Title = "Delete layer",
            Message = "This operation deletes a layer from the drawing.",
            Risk = "High",
        };
    }

    internal sealed class ConfirmationRequiredCommandHandler(FakeWriteRepository repository)
        : ICommandHandler<ConfirmationRequiredCommand, WriteCommandResult>
    {
        public WriteCommandResult Handle(
            ConfirmationRequiredCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
        {
            repository.Writes.Add("confirmed-write");
            return new WriteCommandResult(command.Name, transaction is not null);
        }
    }

    /// <summary>A writing command whose handler throws a domain failure (rollback path).</summary>
    internal sealed class FailingCommand : ICommand<WriteCommandResult>
    {
        public string Name => "fail.write";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
    }

    internal sealed class FailingCommandHandler : ICommandHandler<FailingCommand, WriteCommandResult>
    {
        public WriteCommandResult Handle(
            FailingCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
            => throw new DomainException(DomainErrorCode.TransactionFailed, "simulated database failure");
    }

    /// <summary>A slow writing command used to exercise timeout and cancellation.</summary>
    internal sealed class SlowCommand : ICommand<WriteCommandResult>
    {
        public string Name => "slow.write";
        public CommandPermission RequiredPermission => CommandPermission.ModifyDrawing;
        public bool IsReadOnly => false;
        public bool RequiresConfirmation => false;
        public ConfirmationDescriptor? Confirmation => null;
    }

    internal sealed class SlowCommandHandler : ICommandHandler<SlowCommand, WriteCommandResult>
    {
        public WriteCommandResult Handle(
            SlowCommand command,
            ICommandExecutionContext context,
            IWriteTransaction? transaction,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < 200; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(10);
            }

            return new WriteCommandResult(command.Name, transaction is not null);
        }
    }
}
