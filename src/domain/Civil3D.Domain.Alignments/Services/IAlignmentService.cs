using Civil3D.Domain.Alignments.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Alignments.Services;

/// <summary>
/// Business-facing read-only alignment queries. Translates repository failures into business
/// results: a missing entity is a <see langword="null"/> return value; other domain errors
/// (for example no active document) propagate for the caller to map further.
/// </summary>
public interface IAlignmentService
{
    /// <summary>Returns all alignments in the active drawing.</summary>
    AlignmentCollection GetAll();

    /// <summary>Returns the alignment with the given name (case-insensitive), or <see langword="null"/>.</summary>
    AlignmentInfo? GetByName(string name);

    /// <summary>Returns the alignment with the given id, or <see langword="null"/>.</summary>
    AlignmentInfo? GetById(long id);

    /// <summary>Returns true when an alignment with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of alignments in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<AlignmentInfo> Query(QueryRequest request);
}
