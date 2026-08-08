using Civil3D.Domain.Pipes.Dtos;

using Civil3D.Domain.Query;
namespace Civil3D.Domain.Pipes.Services;

/// <summary>
/// Business-facing read-only pipe network queries. A missing entity is a <see langword="null"/>
/// return value; other domain errors propagate for the caller to map further.
/// </summary>
public interface IPipeService
{
    /// <summary>Returns all pipe networks in the active drawing.</summary>
    PipeNetworkCollection GetAll();

    /// <summary>Returns the network with the given name (case-insensitive), or <see langword="null"/>.</summary>
    PipeNetworkInfo? GetByName(string name);

    /// <summary>Returns the network with the given id, or <see langword="null"/>.</summary>
    PipeNetworkInfo? GetById(long id);

    /// <summary>Returns true when a network with the given name exists (case-insensitive).</summary>
    bool Exists(string name);

    /// <summary>Returns the number of pipe networks in the active drawing.</summary>
    int Count();

    /// <summary>Executes a paged, filtered and sorted query against the active drawing.</summary>
    /// <param name="request">The query request (filters, sorts, paging, field selection).</param>
    PageResult<PipeNetworkInfo> Query(QueryRequest request);
}
