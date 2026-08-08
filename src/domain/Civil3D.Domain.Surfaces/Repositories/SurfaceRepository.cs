using Civil3D.Domain.Data;
using Civil3D.Domain.Surfaces.Data;
using Civil3D.Domain.Repositories;
using Civil3D.Domain.Surfaces.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Surfaces.Repositories;

/// <summary>
/// Read-only surface repository. Delegates the Autodesk read to an
/// <see cref="ISurfaceDataSource"/> (one read-only transaction per call) and applies the
/// standard repository exception handling.
/// </summary>
public sealed class SurfaceRepository : ReadOnlyRepositoryBase, ISurfaceRepository
{
    private readonly ISurfaceDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The surface data source (Autodesk implementation in production, a fake in tests).</param>
    public SurfaceRepository(ISurfaceDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public SurfaceCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public SurfaceInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)),
            "surface"));
    }

    /// <inheritdoc />
    public SurfaceInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(s => s.Id == id),
            "surface"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public bool ExistsName(string name, long? exceptId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || s.Id != exceptId)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<SurfaceInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
