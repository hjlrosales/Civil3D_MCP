namespace Civil3D.Tools.Editing.Dtos;

/// <summary>
/// Input of <c>delete_pipe</c>: the stable numeric id of the pipe to remove from its network.
/// </summary>
public sealed record DeletePipeRequest
{
    /// <summary>
    /// Stable numeric id of the pipe to delete, as returned by <c>create_pipe</c> or
    /// <c>list_pipe_networks</c>.
    /// </summary>
    public long PipeId { get; init; }
}
