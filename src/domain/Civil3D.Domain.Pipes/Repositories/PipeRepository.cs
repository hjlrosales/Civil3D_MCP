using Civil3D.Domain.Data;
using Civil3D.Domain.Pipes.Data;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Read-only pipe network repository. Delegates the Autodesk read to an
/// <see cref="IPipeDataSource"/> (one read-only transaction per call) and applies the standard
/// repository exception handling.
/// </summary>
public sealed class PipeRepository : ReadOnlyRepositoryBase, IPipeRepository
{
    private readonly IPipeDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The pipe data source (Autodesk implementation in production, a fake in tests).</param>
    public PipeRepository(IPipeDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public PipeNetworkCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public PipeNetworkInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(n =>
                string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)),
            "pipe network"));
    }

    /// <inheritdoc />
    public PipeNetworkInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(n => n.Id == id),
            "pipe network"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(n =>
            string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<PipeNetworkInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
