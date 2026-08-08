using Civil3D.Domain.Styles.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Styles.Repositories;

/// <summary>
/// Read-only repository for Civil 3D styles. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface IStyleRepository
{
    /// <summary>Returns all styles in the active drawing, read once.</summary>
    StyleCollection GetAll();

    /// <summary>Returns the style with the given name (case-insensitive) or throws <c>EntityNotFound</c>.</summary>
    StyleInfo GetByName(string name);

    /// <summary>Returns the style with the given id or throws <c>EntityNotFound</c>.</summary>
    StyleInfo GetById(long id);

    /// <summary>Returns true when a style with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of styles in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<StyleInfo> Query(QueryRequest request);
}
