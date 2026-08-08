using Civil3D.Domain.Alignments.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Alignments.Repositories;

/// <summary>
/// Read-only repository for alignments. Methods either return immutable DTOs or throw
/// <see cref="Civil3D.Domain.Errors.DomainException"/> (no active document, entity not found,
/// transaction failure). No method edits the drawing.
/// </summary>
public interface IAlignmentRepository
{
    /// <summary>Returns all alignments in the active drawing, read once.</summary>
    AlignmentCollection GetAll();

    /// <summary>
    /// Returns the alignment with the given name (case-insensitive) or throws
    /// <c>EntityNotFound</c>.
    /// </summary>
    AlignmentInfo GetByName(string name);

    /// <summary>
    /// Returns the alignment with the given id or throws <c>EntityNotFound</c>.
    /// </summary>
    AlignmentInfo GetById(long id);

    /// <summary>Returns true when an alignment with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>
    /// Returns true when an alignment with the given name exists, optionally ignoring one id
    /// (used by rename to exclude the object being renamed). Case-insensitive.
    /// </summary>
    bool ExistsName(string name, long? exceptId = null);

    /// <summary>Returns the number of alignments in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<AlignmentInfo> Query(QueryRequest request);
}
