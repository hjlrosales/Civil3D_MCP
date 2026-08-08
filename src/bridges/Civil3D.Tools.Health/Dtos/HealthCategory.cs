namespace Civil3D.Tools.Health.Dtos;

/// <summary>
/// Aggregated severity counts for one finding category (for example <c>Alignments</c> or
/// <c>Corridors</c>). Produced by the analyzer so callers can drill into the report by category.
/// </summary>
public sealed record HealthCategory
{
    /// <summary>The category name, matching <see cref="HealthIssue.Category"/>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The number of findings in this category.</summary>
    public int TotalIssues { get; init; }

    /// <summary>The number of information findings in this category.</summary>
    public int InformationCount { get; init; }

    /// <summary>The number of warning findings in this category.</summary>
    public int WarningCount { get; init; }

    /// <summary>The number of error findings in this category.</summary>
    public int ErrorCount { get; init; }

    /// <summary>The number of critical findings in this category.</summary>
    public int CriticalCount { get; init; }
}
