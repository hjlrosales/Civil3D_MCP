using Civil3D.Domain.Pipes.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Pipes.Repositories;

/// <summary>
/// Read-only repository for pipe networks and their parts. Methods either return immutable DTOs
/// or throw <see cref="Civil3D.Domain.Errors.DomainException"/>. No method edits the drawing.
/// </summary>
public interface IPipeRepository
{
    /// <summary>Returns all pipe networks in the active drawing, read once.</summary>
    PipeNetworkCollection GetAll();

    /// <summary>Returns the network with the given name (case-insensitive) or throws <c>EntityNotFound</c>.</summary>
    PipeNetworkInfo GetByName(string name);

    /// <summary>Returns the network with the given id or throws <c>EntityNotFound</c>.</summary>
    PipeNetworkInfo GetById(long id);

    /// <summary>Returns true when a network with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of pipe networks in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<PipeNetworkInfo> Query(QueryRequest request);
}
