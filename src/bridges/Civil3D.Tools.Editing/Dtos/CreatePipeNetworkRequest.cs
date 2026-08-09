namespace Civil3D.Tools.Editing.Dtos;

/// <summary>
/// Input of <c>create_pipe_network</c>: the network name, an optional description, an optional
/// parts-list name, and the pipe materials whose part families are added to the parts list from
/// the installed Civil 3D pipe catalog.
/// </summary>
public sealed record CreatePipeNetworkRequest
{
    /// <summary>Name of the pipe network to create. Must not already exist in the drawing.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional free-text description to set on the network.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Name of the parts list to use (created when it does not exist); when omitted, a parts list
    /// derived from the network name is created.
    /// </summary>
    public string? PartsListName { get; init; }

    /// <summary>
    /// Pipe materials whose part families are added to the parts list (for example "HDPE", "PVC",
    /// "Concrete", "Ductile Iron"). Defaults to the common gravity-pipe materials when omitted.
    /// </summary>
    public string[]? Materials { get; init; }

    /// <summary>
    /// Nominal inner diameters (millimetres) to add as sizes to every added pipe part family (for
    /// example [200] adds a 200 mm size), so a later <c>create_pipe</c> can snap to that diameter.
    /// When omitted, the common gravity-pipe range 100–300 mm is added.
    /// </summary>
    public double[]? SizesMm { get; init; }
}
