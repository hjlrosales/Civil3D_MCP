using System.Text.Json;
using System.Xml.Linq;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Bridge.Execution;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Query;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Microsoft.Extensions.Logging.Abstractions;

namespace Civil3D.Tools.Export.Tests;

/// <summary>
/// Shared harness doubles for the export tests: a fake session, in-memory counting domain
/// services, a fake LandXML exporter that can write real temp files, a progress recorder,
/// deliberate sample data and helpers to drive the SDK dispatcher end-to-end.
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

    /// <summary>In-memory alignment service exposing only <c>Count</c>.</summary>
    internal sealed class FakeAlignmentService(int count) : IAlignmentService
    {
        public AlignmentCollection GetAll() => throw new NotImplementedException();
        public AlignmentInfo? GetById(long id) => throw new NotImplementedException();
        public AlignmentInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => count;
        public PageResult<AlignmentInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>In-memory profile service exposing only <c>Count</c>.</summary>
    internal sealed class FakeProfileService(int count) : IProfileService
    {
        public ProfileCollection GetAll() => throw new NotImplementedException();
        public ProfileInfo? GetById(long id) => throw new NotImplementedException();
        public ProfileInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => count;
        public PageResult<ProfileInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>In-memory surface service exposing only <c>Count</c>.</summary>
    internal sealed class FakeSurfaceService(int count) : ISurfaceService
    {
        public SurfaceCollection GetAll() => throw new NotImplementedException();
        public SurfaceInfo? GetById(long id) => throw new NotImplementedException();
        public SurfaceInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => count;
        public PageResult<SurfaceInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>In-memory corridor service exposing only <c>Count</c>.</summary>
    internal sealed class FakeCorridorService(int count) : ICorridorService
    {
        public CorridorCollection GetAll() => throw new NotImplementedException();
        public CorridorInfo? GetById(long id) => throw new NotImplementedException();
        public CorridorInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => count;
        public PageResult<CorridorInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>In-memory pipe network service exposing only <c>Count</c>.</summary>
    internal sealed class FakePipeService(int count) : IPipeService
    {
        public PipeNetworkCollection GetAll() => throw new NotImplementedException();
        public PipeNetworkInfo? GetById(long id) => throw new NotImplementedException();
        public PipeNetworkInfo? GetByName(string name) => throw new NotImplementedException();
        public bool Exists(string name) => throw new NotImplementedException();
        public int Count() => count;
        public PageResult<PipeNetworkInfo> Query(QueryRequest request) => throw new NotImplementedException();
    }

    /// <summary>
    /// Fake exporter. When <paramref name="status"/> is <see cref="LandXmlExportStatus.Exported"/>
    /// and <paramref name="writeFile"/> is true it writes a real well-formed XML file at the
    /// requested path (so the workflow's output-validation stage has something to validate); when
    /// <c>writeFile</c> is false it claims success without writing anything (to exercise the
    /// validation-failure path). Records the data it received for substitution assertions.
    /// </summary>
    internal sealed class FakeLandXmlExporter(
        LandXmlExportStatus status = LandXmlExportStatus.Exported,
        string? reason = null,
        bool writeFile = true,
        int exportedCount = 3,
        int skippedCount = 0) : ILandXmlExporter
    {
        public int Calls { get; private set; }

        public LandXmlExportData? LastData { get; private set; }

        public LandXmlExportResult Export(LandXmlExportData data)
        {
            Calls++;
            LastData = data;

            if (status == LandXmlExportStatus.NotSupported)
            {
                return new LandXmlExportResult
                {
                    Status = LandXmlExportStatus.NotSupported,
                    Reason = reason ?? "Not supported by the installed API.",
                    OutputPath = data.OutputPath,
                    FileSizeBytes = 0,
                    ExportedObjects = [],
                    SkippedObjects = [],
                    CompletedAtUtc = null,
                };
            }

            long size = 0;
            if (writeFile)
            {
                string? directory = Path.GetDirectoryName(data.OutputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                new XDocument(
                    new XElement("LandXML", new XAttribute("version", "1.2"),
                        new XElement("Project", data.OutputPath)))
                    .Save(data.OutputPath);
                size = new FileInfo(data.OutputPath).Length;
            }

            return new LandXmlExportResult
            {
                Status = LandXmlExportStatus.Exported,
                OutputPath = data.OutputPath,
                FileSizeBytes = size,
                ExportedObjects = Enumerable.Range(1, exportedCount)
                    .Select(i => new ExportedObject { Type = "Alignment", Name = $"AL{i}", Id = i })
                    .ToArray(),
                SkippedObjects = skippedCount == 0
                    ? []
                    : Enumerable.Range(1, skippedCount)
                        .Select(i => new SkippedObject
                        {
                            Type = "Corridor",
                            Name = $"CR{i}",
                            Id = i,
                            Reason = "Not supported by the installed API.",
                        })
                        .ToArray(),
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }
    }

    /// <summary>Deliberate sample data: a drawing, per-type counts and temp output paths.</summary>
    internal static class SampleData
    {
        internal static ActiveDrawing Drawing() => new()
        {
            DrawingName = "ExportSample.dwg",
            DrawingPath = @"C:\Drawings\ExportSample.dwg",
            DrawingVersion = "AC1032",
            IsModified = false,
            IsReadOnly = false,
            CurrentLayout = "Model",
            IsModelSpaceActive = true,
            DatabaseFingerprint = "fp-export",
            Civil3DVersion = "25.0",
            OpenDocumentsCount = 1,
            CurrentDocumentName = "ExportSample.dwg",
            CurrentDocumentPath = @"C:\Drawings\ExportSample.dwg",
        };

        /// <summary>A unique temp output path for one test run.</summary>
        internal static string TempXmlPath()
            => Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid():N}.xml");

        internal static FakeAlignmentService Alignments() => new(2);
        internal static FakeProfileService Profiles() => new(3);
        internal static FakeSurfaceService Surfaces() => new(1);
        internal static FakeCorridorService Corridors() => new(0);
        internal static FakePipeService PipeNetworks() => new(1);
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
        CorrelationId = "c-export",
        SessionId = "s-export",
        TimeoutMilliseconds = timeoutMs,
    };
}
