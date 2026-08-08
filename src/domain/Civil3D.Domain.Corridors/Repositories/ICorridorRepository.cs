using Civil3D.Domain.Corridors.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Corridors.Repositories;

/// <summary>
/// Read-only repository for corridors. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface ICorridorRepository
{
    /// <summary>Returns all corridors in the active drawing, read once.</summary>
    CorridorCollection GetAll();

    /// <summary>Returns the corridor with the given name (case-insensitive) or throws <c>EntityNotFound</c>.</summary>
    CorridorInfo GetByName(string name);

    /// <summary>Returns the corridor with the given id or throws <c>EntityNotFound</c>.</summary>
    CorridorInfo GetById(long id);

    /// <summary>Returns true when a corridor with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of corridors in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<CorridorInfo> Query(QueryRequest request);
}
