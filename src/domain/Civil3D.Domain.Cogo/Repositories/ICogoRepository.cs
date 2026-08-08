using Civil3D.Domain.Cogo.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Cogo.Repositories;

/// <summary>
/// Read-only repository for COGO points. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface ICogoRepository
{
    /// <summary>Returns all COGO points in the active drawing, read once.</summary>
    CogoPointCollection GetAll();

    /// <summary>Returns the point with the given point number or throws <c>EntityNotFound</c>.</summary>
    CogoPointInfo GetByPointNumber(uint pointNumber);

    /// <summary>Returns the point with the given id or throws <c>EntityNotFound</c>.</summary>
    CogoPointInfo GetById(long id);

    /// <summary>Returns true when a point with the given number exists.</summary>
    bool Exists(uint pointNumber);

    /// <summary>Returns the number of COGO points in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<CogoPointInfo> Query(QueryRequest request);
}
