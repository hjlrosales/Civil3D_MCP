using Civil3D.Domain.Styles.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Styles.Services;

/// <summary>
/// Business-facing read-only style queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface IStyleService
{
    /// <summary>Returns all styles in the active drawing.</summary>
    StyleCollection GetAll();

    /// <summary>Returns the style with the given name (case-insensitive), or <see langword="null"/>.</summary>
    StyleInfo? GetByName(string name);

    /// <summary>Returns the style with the given id, or <see langword="null"/>.</summary>
    StyleInfo? GetById(long id);

    /// <summary>Returns true when a style with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of styles in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<StyleInfo> Query(QueryRequest request);
}
