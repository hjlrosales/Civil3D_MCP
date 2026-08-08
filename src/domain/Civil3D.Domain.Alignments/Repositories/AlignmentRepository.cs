using Civil3D.Domain.Alignments.Data;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Data;
using Civil3D.Domain.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Alignments.Repositories;

/// <summary>
/// Read-only alignment repository. Delegates the Autodesk read to an
/// <see cref="IAlignmentDataSource"/> (one read-only transaction per call) and applies the
/// standard repository exception handling. No editing and no Autodesk types in the public surface.
/// </summary>
public sealed class AlignmentRepository : ReadOnlyRepositoryBase, IAlignmentRepository
{
    private readonly IAlignmentDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The alignment data source (Autodesk implementation in production, a fake in tests).</param>
    public AlignmentRepository(IAlignmentDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public AlignmentCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public AlignmentInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)),
            "alignment"));
    }

    /// <inheritdoc />
    public AlignmentInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(a => a.Id == id),
            "alignment"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public bool ExistsName(string name, long? exceptId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || a.Id != exceptId)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<AlignmentInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
