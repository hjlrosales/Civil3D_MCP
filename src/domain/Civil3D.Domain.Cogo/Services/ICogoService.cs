using Civil3D.Domain.Cogo.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Cogo.Services;

/// <summary>
/// Business-facing read-only COGO point queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface ICogoService
{
    /// <summary>Returns all COGO points in the active drawing.</summary>
    CogoPointCollection GetAll();

    /// <summary>Returns the point with the given point number, or <see langword="null"/>.</summary>
    CogoPointInfo? GetByPointNumber(uint pointNumber);

    /// <summary>Returns the point with the given id, or <see langword="null"/>.</summary>
    CogoPointInfo? GetById(long id);

    /// <summary>Returns true when a point with the given number exists.</summary>
    bool Exists(uint pointNumber);

    /// <summary>Returns the number of COGO points in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<CogoPointInfo> Query(QueryRequest request);
}
