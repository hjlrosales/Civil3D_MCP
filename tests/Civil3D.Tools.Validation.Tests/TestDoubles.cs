using System.Text.Json;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Query;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// Shared harness doubles for the design-validation tests: a fake session, in-memory domain
/// service fakes with canned collections, a fake statistics service, sample data and helpers to
/// drive the SDK dispatcher end-to-end.
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

    internal sealed class FakeDrawingStatisticsService(DrawingStatistics statistics) : IDrawingStatisticsService
    {
        public int Calls { get; private set; }

        public DrawingStatistics GetStatistics(ActiveDrawing drawing, CancellationToken cancellationToken)
        {
            Calls++;
            return statistics;
        }
    }

    internal sealed class FakeAlignmentService(IReadOnlyList<AlignmentInfo> items) : IAlignmentService
    {
        public AlignmentCollection GetAll() => new(items);
        public AlignmentInfo? GetByName(string name) => throw new NotImplementedException();
        public AlignmentInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<AlignmentInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakeSurfaceService(IReadOnlyList<SurfaceInfo> items) : ISurfaceService
    {
        public SurfaceCollection GetAll() => new(items);
        public SurfaceInfo? GetByName(string name) => throw new NotImplementedException();
        public SurfaceInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<SurfaceInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakeProfileService(IReadOnlyList<ProfileInfo> items) : IProfileService
    {
        public ProfileCollection GetAll() => new(items);
        public ProfileInfo? GetByName(string name) => throw new NotImplementedException();
        public ProfileInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<ProfileInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakeCorridorService(IReadOnlyList<CorridorInfo> items) : ICorridorService
    {
        public CorridorCollection GetAll() => new(items);
        public CorridorInfo? GetByName(string name) => throw new NotImplementedException();
        public CorridorInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<CorridorInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakePipeService(IReadOnlyList<PipeNetworkInfo> items) : IPipeService
    {
        public PipeNetworkCollection GetAll() => new(items);
        public PipeNetworkInfo? GetByName(string name) => throw new NotImplementedException();
        public PipeNetworkInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<PipeNetworkInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakeCogoService(IReadOnlyList<CogoPointInfo> items) : ICogoService
    {
        public CogoPointCollection GetAll() => new(items);
        public CogoPointInfo? GetByPointNumber(uint pointNumber) => throw new NotImplementedException();
        public CogoPointInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(uint pointNumber) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<CogoPointInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    internal sealed class FakeStyleService(IReadOnlyList<StyleInfo> items) : IStyleService
    {
        public StyleCollection GetAll() => new(items);
        public StyleInfo? GetByName(string name) => throw new NotImplementedException();
        public StyleInfo? GetById(long id) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => items.Count;
        public PageResult<StyleInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>Canned sample data engineered to trigger every rule: a duplicate alignment name,
    /// a profile without an alignment, a corridor referencing a missing alignment, an alignment
    /// referencing a missing style, one unused style, one undocumented object, two COGO points
    /// sharing a number, a pipes-only network, and a drawing with content but no surfaces.</summary>
    internal static class SampleData
    {
        internal static ActiveDrawing Drawing() => new()
        {
            DrawingName = "ValidationSample.dwg",
            DrawingPath = @"C:\Drawings\ValidationSample.dwg",
            DrawingVersion = "AC1032",
            IsModified = true,
            IsReadOnly = false,
            CurrentLayout = "Model",
            IsModelSpaceActive = true,
            DatabaseFingerprint = "fp-validation",
            Civil3DVersion = "25.0",
            OpenDocumentsCount = 1,
            CurrentDocumentName = "ValidationSample.dwg",
            CurrentDocumentPath = @"C:\Drawings\ValidationSample.dwg",
        };

        internal static DrawingStatistics Statistics() => new()
        {
            LayerCount = 12,
            BlockCount = 25,
            XRefCount = 2,
            EntityCount = 3_400,
            ModelSpaceEntityCount = 2_100,
            PaperSpaceEntityCount = 1_300,
            ViewportCount = 3,
            TextStyleCount = 5,
            DimensionStyleCount = 4,
            LinetypeCount = 6,
            RegisteredApplicationCount = 8,
            DictionaryCount = 10,
            ApproximateDrawingSizeBytes = 2_500_000,
        };

        internal static IReadOnlyList<AlignmentInfo> Alignments() =>
        [
            new() { Id = 1, Name = "Main Road", Description = "Primary corridor alignment", StyleId = 1 },
            new() { Id = 2, Name = "Main Road", Description = null, StyleId = 99 }, // duplicate name + missing style
        ];

        internal static IReadOnlyList<SurfaceInfo> Surfaces() => Array.Empty<SurfaceInfo>(); // empty where unexpected

        internal static IReadOnlyList<ProfileInfo> Profiles() =>
        [
            new() { Id = 1, Name = "FG", Description = "Finished grade", AlignmentId = 1 },
            new() { Id = 2, Name = "Orphan", Description = null, AlignmentId = 0 }, // no owning alignment
            new() { Id = 3, Name = "Ghost", AlignmentId = 42 }, // unresolved alignment reference
        ];

        internal static IReadOnlyList<CorridorInfo> Corridors() =>
        [
            new() { Id = 1, Name = "Main Corridor", Description = "Main corridor model", AlignmentId = 1, StyleId = 1, CodeSetStyleId = 1 },
        ];

        internal static IReadOnlyList<PipeNetworkInfo> PipeNetworks() =>
        [
            new() { Id = 1, Name = "Storm", Description = "Storm network", Pipes = [new PipeInfo { Id = 1, Name = "P-1" }] }, // no structures
        ];

        internal static IReadOnlyList<CogoPointInfo> CogoPoints() =>
        [
            new() { Id = 1, PointNumber = 1, FullDescription = "Setout point" },
            new() { Id = 2, PointNumber = 1, FullDescription = null }, // duplicate number + no description
        ];

        internal static IReadOnlyList<StyleInfo> Styles() =>
        [
            new() { Id = 1, Name = "Road Style", Description = "Road alignment style", Kind = StyleKind.Alignment },
            new() { Id = 2, Name = "Unused Style", Description = null, Kind = StyleKind.Alignment },
        ];
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
        CorrelationId = "c-validation",
        SessionId = "s-validation",
        TimeoutMilliseconds = timeoutMs,
    };
}
