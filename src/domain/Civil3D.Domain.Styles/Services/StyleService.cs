using Civil3D.Domain.Services;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Styles.Repositories;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Styles.Services;

/// <summary>
/// Style service: thin orchestration over <see cref="IStyleRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class StyleService : DomainServiceBase, IStyleService
{
    private readonly IStyleRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The style repository.</param>
    public StyleService(IStyleRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public StyleCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public StyleInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public StyleInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<StyleInfo> Query(QueryRequest request) => _repository.Query(request);
}
