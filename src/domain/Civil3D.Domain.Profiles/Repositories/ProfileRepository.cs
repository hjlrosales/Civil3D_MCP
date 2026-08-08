using Civil3D.Domain.Data;
using Civil3D.Domain.Profiles.Data;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Profiles.Repositories;

/// <summary>
/// Read-only profile repository. Delegates the Autodesk read to an
/// <see cref="IProfileDataSource"/> (one read-only transaction per call) and applies the
/// standard repository exception handling.
/// </summary>
public sealed class ProfileRepository : ReadOnlyRepositoryBase, IProfileRepository
{
    private readonly IProfileDataSource _dataSource;

    /// <summary>Creates the repository over the given data source.</summary>
    /// <param name="dataSource">The profile data source (Autodesk implementation in production, a fake in tests).</param>
    public ProfileRepository(IProfileDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public ProfileCollection GetAll()
        => ExecuteRead(() => _dataSource.ReadAll());

    /// <inheritdoc />
    public ProfileInfo GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)),
            "profile"));
    }

    /// <inheritdoc />
    public ProfileInfo GetById(long id)
        => ExecuteRead(() => RequireResult(
            _dataSource.ReadAll().Items.FirstOrDefault(p => p.Id == id),
            "profile"));

    /// <inheritdoc />
    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ExecuteRead(() => _dataSource.ReadAll().Items.Any(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public int Count()
        => ExecuteRead(() => _dataSource.ReadAll().Count);

    /// <inheritdoc />
    public PageResult<ProfileInfo> Query(QueryRequest request)
        => ExecuteRead(() => QueryEngine.Apply(_dataSource.ReadAll().Items, request));
}
