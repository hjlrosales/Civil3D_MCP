using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Commands.Tests;

/// <summary>
/// Shared harness for the command tool tests: a real <see cref="CommandDispatcher"/> and
/// <see cref="TransactionPipeline"/> over an in-memory transaction provider, a fake session,
/// and helpers to drive the SDK dispatcher end-to-end.
/// </summary>
internal static class TestDoubles
{
    internal sealed class FakeSession : ICivil3DSession
    {
        private readonly ActiveDrawing? _drawing;

        public FakeSession(ActiveDrawing? drawing) => _drawing = drawing;

        public ActiveDrawing? GetActiveDrawing() => _drawing;
    }

    /// <summary>Runs the action inline, mimicking the application-context marshaler.</summary>
    internal sealed class InlineContext : IApplicationContext
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) => action();
    }

    /// <summary>In-memory write repository the test handlers write to.</summary>
    internal sealed class FakeWriteRepository
    {
        public List<string> Entries { get; } = [];
    }

    /// <summary>A write transaction provider that records begun transactions.</summary>
    internal sealed class RecordingTransactionProvider : ITransactionProvider
    {
        public List<FakeWriteTransaction> Begun { get; } = [];

        public IWriteTransaction Begin(string commandName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = new FakeWriteTransaction(commandName);
            Begun.Add(transaction);
            return transaction;
        }
    }

    internal sealed class FakeWriteTransaction(string commandName) : IWriteTransaction
    {
        /// <summary>The fake Autodesk handle, tagged with the command name for assertions.</summary>
        public object? Handle { get; } = $"fake:{commandName}";
        public bool IsCommitted { get; private set; }
        public bool IsRolledBack { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Commit()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Commit after commit.");
            }

            IsCommitted = true;
        }

        public void Rollback()
        {
            if (IsCommitted)
            {
                throw new CommandException(CommandErrorCode.TransactionFailed, "Rollback after commit.");
            }

            IsRolledBack = true;
        }

        public void Dispose() => IsDisposed = true;
    }

    /// <summary>Grants confirmation for every command (for confirmation tests).</summary>
    internal sealed class GrantingConfirmationGate : IConfirmationGate
    {
        public bool IsGranted(ICommand command, string correlationId) => true;
    }

    internal static ActiveDrawing SampleDrawing() => new()
    {
        DrawingName = "CommandsSample.dwg",
        DrawingPath = @"C:\Drawings\CommandsSample.dwg",
        DrawingVersion = "AC1032",
        IsModified = false,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-commands",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 1,
        CurrentDocumentName = "CommandsSample.dwg",
        CurrentDocumentPath = @"C:\Drawings\CommandsSample.dwg",
    };

    internal static ToolDispatcher CreateDispatcher(ToolCatalog catalog)
    {
        var dispatcher = new ToolDispatcher(
            catalog,
            new InlineContext(),
            new CancellationRegistry(),
            NullLogger<ToolDispatcher>.Instance);
        dispatcher.Start();
        return dispatcher;
    }

    internal static ToolInvocation Invoke(string tool, object? parameters = null) => new()
    {
        ToolName = tool,
        Parameters = parameters is null ? null : JsonSerializer.SerializeToElement(parameters, SharedJson.Options),
        CorrelationId = "c-command",
        SessionId = "s-command",
        TimeoutMilliseconds = 10_000,
    };
}
