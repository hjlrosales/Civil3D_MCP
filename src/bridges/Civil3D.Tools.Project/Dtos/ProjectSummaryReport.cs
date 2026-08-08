namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The <c>project_summary_report</c> result: a comprehensive read-only overview of the active
/// Civil 3D drawing, combining drawing metadata, object inventory, reference integrity,
/// complexity classification, statistics and recommendations. Immutable and Autodesk-free.
/// </summary>
public sealed record ProjectSummaryReport
{
    /// <summary>The drawing metadata section.</summary>
    public ProjectOverview Overview { get; init; } = new();

    /// <summary>The object inventory section.</summary>
    public ObjectInventory Inventory { get; init; } = new();

    /// <summary>The reference integrity section.</summary>
    public ReferenceSummary References { get; init; } = new();

    /// <summary>The complexity classification section.</summary>
    public ComplexityAssessment Complexity { get; init; } = new();

    /// <summary>The top-level totals.</summary>
    public ProjectStatistics Statistics { get; init; } = new();

    /// <summary>Recommended next steps, ordered by priority (highest first).</summary>
    public IReadOnlyList<ProjectRecommendation> Recommendations { get; init; } = Array.Empty<ProjectRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
