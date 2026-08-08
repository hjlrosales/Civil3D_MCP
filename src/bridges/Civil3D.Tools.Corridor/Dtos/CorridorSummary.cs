namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// One analyzed corridor: its identity and every metric the domain layer exposes (baseline and
/// corridor-surface counts, primary alignment id, style ids) plus a short health status.
/// </summary>
public sealed record CorridorSummary
{
    /// <summary>The corridor id.</summary>
    public long Id { get; init; }

    /// <summary>The corridor name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The corridor description, or <see langword="null"/> when empty.</summary>
    public string? Description { get; init; }

    /// <summary>The id of the primary baseline alignment, or <see langword="null"/>.</summary>
    public long? AlignmentId { get; init; }

    /// <summary>The id of the corridor style, or <see langword="null"/>.</summary>
    public long? StyleId { get; init; }

    /// <summary>The id of the code set style, or <see langword="null"/>.</summary>
    public long? CodeSetStyleId { get; init; }

    /// <summary>Number of baselines in the corridor.</summary>
    public int BaselineCount { get; init; }

    /// <summary>Number of corridor surfaces built on the corridor.</summary>
    public int CorridorSurfaceCount { get; init; }

    /// <summary>A short health status, for example <c>Healthy</c> or <c>No Baselines</c>.</summary>
    public string Status { get; init; } = string.Empty;
}
