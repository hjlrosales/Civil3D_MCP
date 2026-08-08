using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Cogo.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Cogo.Services;

/// <summary>
/// COGO point service: thin orchestration over <see cref="ICogoRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class CogoService : DomainServiceBase, ICogoService
{
    private readonly ICogoRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The COGO repository.</param>
    public CogoService(ICogoRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public CogoPointCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public CogoPointInfo? GetByPointNumber(uint pointNumber) => NotFoundAsNull(() => _repository.GetByPointNumber(pointNumber));

    /// <inheritdoc />
    public CogoPointInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(uint pointNumber) => _repository.Exists(pointNumber);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<CogoPointInfo> Query(QueryRequest request) => _repository.Query(request);
}
