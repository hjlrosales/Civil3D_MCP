namespace Civil3D.Tools.Health.Dtos;

/// <summary>
/// Severity roll-up of the findings plus the total number of domain objects inspected, so callers
/// can gauge both the volume of problems and the size of the drawing's object population.
/// </summary>
public sealed record HealthStatistics
{
    /// <summary>The total number of findings.</summary>
    public int TotalIssues { get; init; }

    /// <summary>The number of information findings.</summary>
    public int InformationCount { get; init; }

    /// <summary>The number of warning findings.</summary>
    public int WarningCount { get; init; }

    /// <summary>The number of error findings.</summary>
    public int ErrorCount { get; init; }

    /// <summary>The number of critical findings.</summary>
    public int CriticalCount { get; init; }

    /// <summary>The total number of domain objects inspected across all collections.</summary>
    public int ObjectCount { get; init; }
}
