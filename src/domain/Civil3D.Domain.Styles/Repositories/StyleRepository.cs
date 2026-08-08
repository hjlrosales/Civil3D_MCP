using Civil3D.Domain.Data;
using Civil3D.Domain.Styles.Data;
using Civil3D.Domain.Repositories;
using Civil3D.Domain.Styles.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Styles.Repositories;

/// <summary>
/// Read-only style repository. Delegates the Autodesk read to an
/// <see cref="IStyleDataSource"/> (one read-only transaction per call) and applies the standard
/// repository exception handling.
/// </summary>
public sealed class StyleRepository : ReadOnlyRepositoryBase, IStyleRepository
{
    private readonly IStyleDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The style data source (Autodesk implementation in production, a fake in tests).</param>
    public StyleRepository(IStyleDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public StyleCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public StyleInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)),
            "style"));
    }

    /// <inheritdoc />
    public StyleInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(s => s.Id == id),
            "style"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<StyleInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
