using Civil3D.Domain.Corridors.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Corridors.Services;

/// <summary>
/// Business-facing read-only corridor queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface ICorridorService
{
    /// <summary>Returns all corridors in the active drawing.</summary>
    CorridorCollection GetAll();

    /// <summary>Returns the corridor with the given name (case-insensitive), or <see langword="null"/>.</summary>
    CorridorInfo? GetByName(string name);

    /// <summary>Returns the corridor with the given id, or <see langword="null"/>.</summary>
    CorridorInfo? GetById(long id);

    /// <summary>Returns true when a corridor with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of corridors in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<CorridorInfo> Query(QueryRequest request);
}
