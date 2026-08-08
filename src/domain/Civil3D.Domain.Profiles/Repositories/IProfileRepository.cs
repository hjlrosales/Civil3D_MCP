using Civil3D.Domain.Profiles.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Profiles.Repositories;

/// <summary>
/// Read-only repository for profiles. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Returns all profiles in the active drawing, read once.</summary>
    ProfileCollection GetAll();

    /// <summary>Returns the profile with the given name (case-insensitive) or throws <c>EntityNotFound</c>.</summary>
    ProfileInfo GetByName(string name);

    /// <summary>Returns the profile with the given id or throws <c>EntityNotFound</c>.</summary>
    ProfileInfo GetById(long id);

    /// <summary>Returns true when a profile with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of profiles in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<ProfileInfo> Query(QueryRequest request);
}
