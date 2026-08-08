using Civil3D.Domain.Surfaces.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Surfaces.Repositories;

/// <summary>
/// Read-only repository for surfaces. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface ISurfaceRepository
{
    /// <summary>Returns all surfaces in the active drawing, read once.</summary>
    SurfaceCollection GetAll();

    /// <summary>Returns the surface with the given name (case-insensitive) or throws <c>EntityNotFound</c>.</summary>
    SurfaceInfo GetByName(string name);

    /// <summary>Returns the surface with the given id or throws <c>EntityNotFound</c>.</summary>
    SurfaceInfo GetById(long id);

    /// <summary>Returns true when a surface with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>
    /// Returns true when a surface with the given name exists, optionally ignoring one id
    /// (used by rename to exclude the object being renamed). Case-insensitive.
    /// </summary>
    bool ExistsName(string name, long? exceptId = null);

    /// <summary>Returns the number of surfaces in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<SurfaceInfo> Query(QueryRequest request);
}
