using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Query;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Surface.Tests;

/// <summary>
/// Shared harness doubles for the surface-comparison tests: a fake session, an in-memory
/// surface service with canned data, a progress recorder, deliberate sample surfaces and
/// helpers to drive the SDK dispatcher end-to-end.
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

    /// <summary>In-memory surface service over a canned list; only <c>GetById</c> is exercised.</summary>
    internal sealed class FakeSurfaceService(IReadOnlyList<SurfaceInfo> items) : ISurfaceService
    {
        public SurfaceCollection GetAll() => new(items);

        public SurfaceInfo? GetById(long id) => items.FirstOrDefault(s => s.Id == id);

        public SurfaceInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<SurfaceInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>
    /// Deliberate sample surfaces: a contrasting pair (EG vs FG — 30% point-count drop, 4.5/10/7
    /// elevation deltas, 5.5 range delta), a near-identical compatible pair, an outdated pair and
    /// an identical pair. Thresholds: point ratio 0.25, range tolerance 2.0, mean tolerance 1.0,
    /// outdated ratio 0.5.
    /// </summary>
    internal static class SampleData
    {
        internal static ActiveDrawing Drawing() => new()
        {
            DrawingName = "SurfaceSample.dwg",
            DrawingPath = @"C:\Drawings\SurfaceSample.dwg",
            DrawingVersion = "AC1032",
            IsModified = false,
            IsReadOnly = false,
            CurrentLayout = "Model",
            IsModelSpaceActive = true,
            DatabaseFingerprint = "fp-surface",
            Civil3DVersion = "25.0",
            OpenDocumentsCount = 1,
            CurrentDocumentName = "SurfaceSample.dwg",
            CurrentDocumentPath = @"C:\Drawings\SurfaceSample.dwg",
        };

        internal static SurfaceInfo Existing() => new()
        {
            Id = 1,
            Name = "EG",
            Description = "Existing ground",
            Kind = SurfaceKind.Tin,
            PointCount = 100_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static SurfaceInfo Proposed() => new()
        {
            Id = 2,
            Name = "FG",
            Description = "Finished grade",
            Kind = SurfaceKind.Tin,
            PointCount = 70_000,
            MinimumElevation = 104.5,
            MaximumElevation = 260.0,
            MeanElevation = 182.0,
        };

        internal static IReadOnlyList<SurfaceInfo> Contrasting() => [Existing(), Proposed()];

        internal static SurfaceInfo CompatibleExisting() => new()
        {
            Id = 3,
            Name = "EG-Copy",
            Kind = SurfaceKind.Tin,
            PointCount = 100_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static SurfaceInfo CompatibleProposed() => new()
        {
            Id = 4,
            Name = "EG-Proposed",
            Kind = SurfaceKind.Tin,
            PointCount = 100_050,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static SurfaceInfo OutdatedExisting() => new()
        {
            Id = 5,
            Name = "EG-Full",
            Kind = SurfaceKind.Tin,
            PointCount = 100_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static SurfaceInfo OutdatedProposed() => new()
        {
            Id = 6,
            Name = "EG-Draft",
            Kind = SurfaceKind.Tin,
            PointCount = 30_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };
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
        CorrelationId = "c-surface",
        SessionId = "s-surface",
        TimeoutMilliseconds = timeoutMs,
    };
}
