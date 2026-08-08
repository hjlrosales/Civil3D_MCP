using Civil3D.Domain.Surfaces.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Surfaces.Services;

/// <summary>
/// Business-facing read-only surface queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface ISurfaceService
{
    /// <summary>Returns all surfaces in the active drawing.</summary>
    SurfaceCollection GetAll();

    /// <summary>Returns the surface with the given name (case-insensitive), or <see langword="null"/>.</summary>
    SurfaceInfo? GetByName(string name);

    /// <summary>Returns the surface with the given id, or <see langword="null"/>.</summary>
    SurfaceInfo? GetById(long id);

    /// <summary>Returns true when a surface with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of surfaces in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<SurfaceInfo> Query(QueryRequest request);
}
