namespace Civil3D.Domain.Pipes.Dtos;

/// <summary>
/// Autodesk-free description of a pipe network to create, resolved by the tool layer from the
/// request: the network name, an optional description, an optional parts-list name, and the
/// human-readable pipe materials whose part families should be added to the parts list. Consumed
/// by <c>ICreatePipeNetworkService</c> and <c>IPipeNetworkCreateRepository</c>.
/// </summary>
public sealed record CreatePipeNetworkSpecification
{
    /// <summary>Name of the pipe network to create. Must not already exist in the drawing.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description to set on the network.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Name of the parts list to use (created when it does not exist); when blank, a parts list
    /// derived from the network name is created.
    /// </summary>
    public string? PartsListName { get; init; }

    /// <summary>
    /// Human-readable pipe materials (for example "HDPE", "PVC", "Concrete") whose part families
    /// are added to the parts list from the installed Civil 3D pipe catalog. Materials without a
    /// matching catalog family are skipped and reported in the outcome.
    /// </summary>
    public IReadOnlyList<string> Materials { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Nominal inner diameters (millimetres) to add as sizes to every added pipe part family, so
    /// <c>create_pipe</c> can snap to them. Empty adds no sizes (the tool layer supplies a sensible
    /// default range when the request omits them, because a family without sizes cannot be used by
    /// <c>create_pipe</c>).
    /// </summary>
    public IReadOnlyList<double> SizesMm { get; init; } = Array.Empty<double>();
}
