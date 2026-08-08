using Civil3D.Domain.Profiles.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Profiles.Services;

/// <summary>
/// Business-facing read-only profile queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface IProfileService
{
    /// <summary>Returns all profiles in the active drawing.</summary>
    ProfileCollection GetAll();

    /// <summary>Returns the profile with the given name (case-insensitive), or <see langword="null"/>.</summary>
    ProfileInfo? GetByName(string name);

    /// <summary>Returns the profile with the given id, or <see langword="null"/>.</summary>
    ProfileInfo? GetById(long id);

    /// <summary>Returns true when a profile with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of profiles in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<ProfileInfo> Query(QueryRequest request);
}
