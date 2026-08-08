using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Health.Dtos;

/// <summary>
/// The <c>drawing_health_report</c> result: a read-only health summary of the active drawing,
/// combining drawing identity, lightweight drawing statistics, analysis findings and
/// recommendations. Entirely immutable and Autodesk-free.
/// </summary>
public sealed record DrawingHealthReport
{
    /// <summary>The file name of the inspected drawing.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the inspected drawing.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c>.</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>The host Civil 3D version, for example <c>25.0</c>.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>True when the drawing contains unsaved changes.</summary>
    public bool IsModified { get; init; }

    /// <summary>True when the drawing file is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>The name of the currently active layout.</summary>
    public string CurrentLayout { get; init; } = string.Empty;

    /// <summary>A stable fingerprint of the database content, for change detection.</summary>
    public string DatabaseFingerprint { get; init; } = string.Empty;

    /// <summary>The lightweight drawing statistics (layer, block, xref, entity and table counts).</summary>
    public DrawingStatistics Statistics { get; init; } = new();

    /// <summary>The severity roll-up of the findings.</summary>
    public HealthStatistics Health { get; init; } = new();

    /// <summary>Per-category severity roll-ups.</summary>
    public IReadOnlyList<HealthCategory> Categories { get; init; } = Array.Empty<HealthCategory>();

    /// <summary>The findings, ordered by severity then code.</summary>
    public IReadOnlyList<HealthIssue> Issues { get; init; } = Array.Empty<HealthIssue>();

    /// <summary>Top-level recommendations summarising the state of the drawing.</summary>
    public IReadOnlyList<HealthRecommendation> Recommendations { get; init; } = Array.Empty<HealthRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public WorkflowExecutionSummary Execution { get; init; } = new();
}
