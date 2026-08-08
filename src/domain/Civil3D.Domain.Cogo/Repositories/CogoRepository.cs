using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Data;
using Civil3D.Domain.Cogo.Data;
using Civil3D.Domain.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Cogo.Repositories;

/// <summary>
/// Read-only COGO point repository. Delegates the Autodesk read to an
/// <see cref="ICogoDataSource"/> (one read-only transaction per call) and applies the standard
/// repository exception handling.
/// </summary>
public sealed class CogoRepository : ReadOnlyRepositoryBase, ICogoRepository
{
    private readonly ICogoDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The COGO data source (Autodesk implementation in production, a fake in tests).</param>
    public CogoRepository(ICogoDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public CogoPointCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public CogoPointInfo GetByPointNumber(uint pointNumber)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(p => p.PointNumber == pointNumber),
            "COGO point"));

    /// <inheritdoc />
    public CogoPointInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(p => p.Id == id),
            "COGO point"));

    /// <inheritdoc />
    public bool Exists(uint pointNumber)
        => ExecuteRead(() => _dataSource.ReadAll().Items.Any(p => p.PointNumber == pointNumber));

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<CogoPointInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
