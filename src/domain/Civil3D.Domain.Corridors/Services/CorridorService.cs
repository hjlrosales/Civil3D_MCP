using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Corridors.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Corridors.Services;

/// <summary>
/// Corridor service: thin orchestration over <see cref="ICorridorRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class CorridorService : DomainServiceBase, ICorridorService
{
    private readonly ICorridorRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The corridor repository.</param>
    public CorridorService(ICorridorRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public CorridorCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public CorridorInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public CorridorInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<CorridorInfo> Query(QueryRequest request) => _repository.Query(request);
}
