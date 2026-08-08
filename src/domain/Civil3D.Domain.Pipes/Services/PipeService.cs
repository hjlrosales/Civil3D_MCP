using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Pipe network service: thin orchestration over <see cref="IPipeRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class PipeService : DomainServiceBase, IPipeService
{
    private readonly IPipeRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The pipe repository.</param>
    public PipeService(IPipeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public PipeNetworkCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public PipeNetworkInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public PipeNetworkInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<PipeNetworkInfo> Query(QueryRequest request) => _repository.Query(request);
}
