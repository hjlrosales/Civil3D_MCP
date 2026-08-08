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
using Civil3D.Tools.CutFill.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.CutFill.Tests;

/// <summary>
/// Shared harness doubles for the cut/fill tests: a fake session, an in-memory surface service
/// with canned data, a fake volume calculator, a progress recorder, deliberate sample surfaces
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

    /// <summary>Fake calculator returning canned volumes, recording the data it received.</summary>
    internal sealed class FakeCutFillCalculator(CutFillCalculationResult result) : ICutFillCalculator
    {
        public int Calls { get; private set; }

        public CutFillCalculationData? LastData { get; private set; }

        public CutFillCalculationResult Calculate(CutFillCalculationData data)
        {
            Calls++;
            LastData = data;
            return result;
        }
    }

    /// <summary>
    /// Deliberate sample data: a contrasting surface pair (EG 100k points vs FG 70k points with
    /// elevation deltas) and canned volume results. The cut-dominant result (cut 12_000, fill
    /// 4_000, net +8_000, total 16_000 → net ratio 0.50) produces a Predominantly Cut verdict,
    /// a Significant net export recommendation and a surface-quality recommendation; the
    /// balanced result (cut 10_000, fill 9_500, net +500, total 19_500 → net ratio ≈ 0.026)
    /// produces a Balanced Earthwork verdict.
    /// </summary>
    internal static class SampleData
    {
        internal static ActiveDrawing Drawing() => new()
        {
            DrawingName = "CutFillSample.dwg",
            DrawingPath = @"C:\Drawings\CutFillSample.dwg",
            DrawingVersion = "AC1032",
            IsModified = false,
            IsReadOnly = false,
            CurrentLayout = "Model",
            IsModelSpaceActive = true,
            DatabaseFingerprint = "fp-cutfill",
            Civil3DVersion = "25.0",
            OpenDocumentsCount = 1,
            CurrentDocumentName = "CutFillSample.dwg",
            CurrentDocumentPath = @"C:\Drawings\CutFillSample.dwg",
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

        internal static SurfaceInfo BalancedExisting() => new()
        {
            Id = 3,
            Name = "EG-Balanced",
            Kind = SurfaceKind.Tin,
            PointCount = 100_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static SurfaceInfo BalancedProposed() => new()
        {
            Id = 4,
            Name = "FG-Balanced",
            Kind = SurfaceKind.Tin,
            PointCount = 100_000,
            MinimumElevation = 100.0,
            MaximumElevation = 250.0,
            MeanElevation = 175.0,
        };

        internal static CutFillCalculationResult CutDominant() => new()
        {
            Status = CutFillStatus.Computed,
            CutVolume = 12_000,
            FillVolume = 4_000,
            NetVolume = 8_000,
            SurfaceAreaUsed = 25_000,
        };

        internal static CutFillCalculationResult Balanced() => new()
        {
            Status = CutFillStatus.Computed,
            CutVolume = 10_000,
            FillVolume = 9_500,
            NetVolume = 500,
            SurfaceAreaUsed = 25_000,
        };

        internal static CutFillCalculationResult ZeroVolumes() => new()
        {
            Status = CutFillStatus.Computed,
            CutVolume = 0,
            FillVolume = 0,
            NetVolume = 0,
            SurfaceAreaUsed = 25_000,
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
        CorrelationId = "c-cutfill",
        SessionId = "s-cutfill",
        TimeoutMilliseconds = timeoutMs,
    };
}
