namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Autodesk-free description of a pipe to delete, resolved by the tool layer from the request:
/// the stable numeric id of the pipe to remove from its network.
/// </summary>
public sealed record DeletePipeSpecification
{
    /// <summary>
    /// Stable numeric id of the pipe to delete, as returned by <c>create_pipe</c> or
    /// <c>list_pipe_networks</c>.
    /// </summary>
    public long PipeId { get; init; }
}
