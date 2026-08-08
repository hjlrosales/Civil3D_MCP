using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Profiles.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Profiles.Services;

/// <summary>
/// Profile service: thin orchestration over <see cref="IProfileRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class ProfileService : DomainServiceBase, IProfileService
{
    private readonly IProfileRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The profile repository.</param>
    public ProfileService(IProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public ProfileCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public ProfileInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public ProfileInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<ProfileInfo> Query(QueryRequest request) => _repository.Query(request);
}
