using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Services;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Alignments.Services;

/// <summary>
/// Alignment service: thin orchestration over <see cref="IAlignmentRepository"/> with the standard
/// domain error translation (<c>EntityNotFound</c> becomes <see langword="null"/>).
/// </summary>
public sealed class AlignmentService : DomainServiceBase, IAlignmentService
{
    private readonly IAlignmentRepository _repository;

    /// <summary>Creates the service over the repository.</summary>
    /// <param name="repository">The alignment repository.</param>
    public AlignmentService(IAlignmentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public AlignmentCollection GetAll() => _repository.GetAll();

    /// <inheritdoc />
    public AlignmentInfo? GetByName(string name) => NotFoundAsNull(() => _repository.GetByName(name));

    /// <inheritdoc />
    public AlignmentInfo? GetById(long id) => NotFoundAsNull(() => _repository.GetById(id));

    /// <inheritdoc />
    public bool Exists(string name) => _repository.Exists(name);

    /// <inheritdoc />
    public int Count() => _repository.Count();

    /// <inheritdoc />
    public PageResult<AlignmentInfo> Query(QueryRequest request) => _repository.Query(request);
}
