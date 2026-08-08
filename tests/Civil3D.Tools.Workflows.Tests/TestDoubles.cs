using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Workflows.Tests;

/// <summary>
/// Shared harness doubles for the workflow tool tests: a fake session, an inline application
/// context and helpers to drive the SDK dispatcher end-to-end.
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

    internal static ActiveDrawing SampleDrawing() => new()
    {
        DrawingName = "WorkflowSample.dwg",
        DrawingPath = @"C:\Drawings\WorkflowSample.dwg",
        DrawingVersion = "AC1032",
        IsModified = false,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-workflow",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 1,
        CurrentDocumentName = "WorkflowSample.dwg",
        CurrentDocumentPath = @"C:\Drawings\WorkflowSample.dwg",
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

    internal static ToolInvocation Invoke(string tool, object? parameters = null, int timeoutMs = 10_000) => new()
    {
        ToolName = tool,
        Parameters = parameters is null ? null : JsonSerializer.SerializeToElement(parameters, SharedJson.Options),
        CorrelationId = "c-workflow",
        SessionId = "s-workflow",
        TimeoutMilliseconds = timeoutMs,
    };
}
