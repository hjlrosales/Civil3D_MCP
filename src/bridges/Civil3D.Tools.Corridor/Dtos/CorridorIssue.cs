namespace Civil3D.Tools.Corridor.Dtos;

/// <summary>
/// A single health finding about one corridor, derived strictly from the exposed domain metrics.
/// </summary>
public sealed record CorridorIssue
{
    /// <summary>The corridor the issue belongs to.</summary>
    public long CorridorId { get; init; }

    /// <summary>The corridor name, for readable reports.</summary>
    public string CorridorName { get; init; } = string.Empty;

    /// <summary>A stable machine-readable code, for example <c>noBaselines</c>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>A concise title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>A description of the finding.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How important the finding is.</summary>
    public CorridorSeverity Severity { get; init; }
}
