using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Domain.Surfaces.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Surfaces.Services;

/// <summary>
/// Surface service: thin orchestration over <see cref="ISurfaceRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class SurfaceService : DomainServiceBase, ISurfaceService
{
    private readonly ISurfaceRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The surface repository.</param>
    public SurfaceService(ISurfaceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public SurfaceCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public SurfaceInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public SurfaceInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<SurfaceInfo> Query(QueryRequest request) => _repository.Query(request);
}
