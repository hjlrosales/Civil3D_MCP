using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Data;
using Civil3D.Domain.Corridors.Data;
using Civil3D.Domain.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Corridors.Repositories;

/// <summary>
/// Read-only corridor repository. Delegates the Autodesk read to an
/// <see cref="ICorridorDataSource"/> (one read-only transaction per call) and applies the
/// standard repository exception handling.
/// </summary>
public sealed class CorridorRepository : ReadOnlyRepositoryBase, ICorridorRepository
{
    private readonly ICorridorDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The corridor data source (Autodesk implementation in production, a fake in tests).</param>
    public CorridorRepository(ICorridorDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public CorridorCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public CorridorInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)),
            "corridor"));
    }

    /// <inheritdoc />
    public CorridorInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(c => c.Id == id),
            "corridor"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<CorridorInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
