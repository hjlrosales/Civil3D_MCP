using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Corridor.Tests;

/// <summary>
/// Shared harness doubles for the corridor tests: a fake session, an in-memory corridor service
/// with canned data, a progress recorder, deliberate sample corridors and helpers to drive the
/// SDK dispatcher end-to-end.
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

    /// <summary>Records progress reports so tests can assert workflow milestones.</summary>
    internal sealed class RecordingProgressReporter : Civil3D.Domain.Commands.IProgressReporter
    {
        public List<ProgressReport> Reports { get; } = [];

        public void Report(int percent, string? stage = null, string? message = null)
            => Reports.Add(new ProgressReport(percent, stage, message));
    }

    internal sealed record ProgressReport(int Percent, string? Stage, string? Message);

    /// <summary>In-memory corridor service over a canned list; <c>GetAll</c> and <c>GetById</c> only.</summary>
    internal sealed class FakeCorridorService(IReadOnlyList<CorridorInfo> items) : ICorridorService
    {
        public CorridorCollection GetAll() => new(items);

        public CorridorInfo? GetById(long id) => items.FirstOrDefault(c => c.Id == id);

        public CorridorInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<CorridorInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

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
        CorrelationId = "c-corridor",
        SessionId = "s-corridor",
        TimeoutMilliseconds = timeoutMs,
    };
}

/// <summary>
/// Deliberate sample data: three corridors exercising every analyzer branch. Mainline is
/// fully healthy (2 baselines, 1 corridor surface, both styles assigned, description
/// present). Ramp A has no corridor surfaces and no description (Warning-level issues,
/// "No Surfaces" status). Stub has no baselines, no surfaces and no styles at all
/// (Error-level issues, "No Baselines" status) — the set therefore yields an
/// "Attention Required" verdict overall (Stub's no-baselines issue is Error-severity), with
/// missing-style, a missing-code-set-style and a missing-description issue across the three.
/// </summary>
internal static class SampleData
{
    internal static ActiveDrawing Drawing() => new()
    {
        DrawingName = "CorridorSample.dwg",
        DrawingPath = @"C:\Drawings\CorridorSample.dwg",
        DrawingVersion = "AC1032",
        IsModified = false,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-corridor",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 1,
        CurrentDocumentName = "CorridorSample.dwg",
        CurrentDocumentPath = @"C:\Drawings\CorridorSample.dwg",
    };

    internal static CorridorInfo Mainline() => new()
    {
        Id = 1,
        Name = "Mainline",
        Description = "Primary road corridor",
        StyleId = 101,
        CodeSetStyleId = 201,
        AlignmentId = 301,
        BaselineCount = 2,
        CorridorSurfaceCount = 1,
    };

    internal static CorridorInfo RampA() => new()
    {
        Id = 2,
        Name = "Ramp A",
        Description = null,
        StyleId = 102,
        CodeSetStyleId = 202,
        AlignmentId = 302,
        BaselineCount = 1,
        CorridorSurfaceCount = 0,
    };

    internal static CorridorInfo Stub() => new()
    {
        Id = 3,
        Name = "Stub",
        Description = null,
        StyleId = null,
        CodeSetStyleId = null,
        AlignmentId = null,
        BaselineCount = 0,
        CorridorSurfaceCount = 0,
    };

    internal static IReadOnlyList<CorridorInfo> All() => [Mainline(), RampA(), Stub()];

    internal static IReadOnlyList<CorridorInfo> HealthyOnly() => [Mainline()];

    internal static IReadOnlyList<CorridorInfo> None() => [];
}
