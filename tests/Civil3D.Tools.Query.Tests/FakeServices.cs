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

namespace Civil3D.Tools.Query.Tests;

/// <summary>
/// In-memory service implementations standing in for the domain services, plus the session fake
/// and canned sample data. Query is served by the real <see cref="QueryEngine"/>, so tool tests
/// exercise the same filtering/paging logic production uses.
/// </summary>
internal static class FakeServices
{
    internal sealed class FakeSession : ICivil3DSession
    {
        private readonly ActiveDrawing? _drawing;

        public FakeSession(ActiveDrawing? drawing) => _drawing = drawing;

        public ActiveDrawing? GetActiveDrawing() => _drawing;
    }

    internal sealed class FakeAlignmentService : IAlignmentService
    {
        private readonly IReadOnlyList<AlignmentInfo> _items;

        public FakeAlignmentService(IReadOnlyList<AlignmentInfo> items) => _items = items;

        public AlignmentCollection GetAll() => new(_items);
        public AlignmentInfo? GetByName(string name) => _items.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        public AlignmentInfo? GetById(long id) => _items.FirstOrDefault(a => a.Id == id);
        public bool Exists(string name) => _items.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<AlignmentInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakeSurfaceService : ISurfaceService
    {
        private readonly IReadOnlyList<SurfaceInfo> _items;

        public FakeSurfaceService(IReadOnlyList<SurfaceInfo> items) => _items = items;

        public SurfaceCollection GetAll() => new(_items);
        public SurfaceInfo? GetByName(string name) => _items.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        public SurfaceInfo? GetById(long id) => _items.FirstOrDefault(s => s.Id == id);
        public bool Exists(string name) => _items.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<SurfaceInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakeProfileService : IProfileService
    {
        private readonly IReadOnlyList<ProfileInfo> _items;

        public FakeProfileService(IReadOnlyList<ProfileInfo> items) => _items = items;

        public ProfileCollection GetAll() => new(_items);
        public ProfileInfo? GetByName(string name) => _items.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        public ProfileInfo? GetById(long id) => _items.FirstOrDefault(p => p.Id == id);
        public bool Exists(string name) => _items.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<ProfileInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakeCorridorService : ICorridorService
    {
        private readonly IReadOnlyList<CorridorInfo> _items;

        public FakeCorridorService(IReadOnlyList<CorridorInfo> items) => _items = items;

        public CorridorCollection GetAll() => new(_items);
        public CorridorInfo? GetByName(string name) => _items.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        public CorridorInfo? GetById(long id) => _items.FirstOrDefault(c => c.Id == id);
        public bool Exists(string name) => _items.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<CorridorInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakePipeService : IPipeService
    {
        private readonly IReadOnlyList<PipeNetworkInfo> _items;

        public FakePipeService(IReadOnlyList<PipeNetworkInfo> items) => _items = items;

        public PipeNetworkCollection GetAll() => new(_items);
        public PipeNetworkInfo? GetByName(string name) => _items.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
        public PipeNetworkInfo? GetById(long id) => _items.FirstOrDefault(n => n.Id == id);
        public bool Exists(string name) => _items.Any(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<PipeNetworkInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakeCogoService : ICogoService
    {
        private readonly IReadOnlyList<CogoPointInfo> _items;

        public FakeCogoService(IReadOnlyList<CogoPointInfo> items) => _items = items;

        public CogoPointCollection GetAll() => new(_items);
        public CogoPointInfo? GetByPointNumber(uint pointNumber) => _items.FirstOrDefault(p => p.PointNumber == pointNumber);
        public CogoPointInfo? GetById(long id) => _items.FirstOrDefault(p => p.Id == id);
        public bool Exists(uint pointNumber) => _items.Any(p => p.PointNumber == pointNumber);
        public int Count() => _items.Count;
        public PageResult<CogoPointInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }

    internal sealed class FakeStyleService : IStyleService
    {
        private readonly IReadOnlyList<StyleInfo> _items;

        public FakeStyleService(IReadOnlyList<StyleInfo> items) => _items = items;

        public StyleCollection GetAll() => new(_items);
        public StyleInfo? GetByName(string name) => _items.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        public StyleInfo? GetById(long id) => _items.FirstOrDefault(s => s.Id == id);
        public bool Exists(string name) => _items.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        public int Count() => _items.Count;
        public PageResult<StyleInfo> Query(QueryRequest request) => QueryEngine.Apply(_items, request);
    }
}
