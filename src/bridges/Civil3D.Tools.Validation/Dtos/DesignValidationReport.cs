using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Validation.Dtos;

/// <summary>
/// The <c>design_validation_report</c> result: a consolidated read-only validation summary of the
/// active drawing, combining drawing identity, the lightweight drawing statistics, per-category
/// severity roll-ups, the findings produced by the registered validation rules, top-level
/// recommendations and execution accounting. Entirely immutable and Autodesk-free.
/// </summary>
public sealed record DesignValidationReport
{
    /// <summary>The file name of the inspected drawing.</summary>
    public string DrawingName { get; init; } = string.Empty;

    /// <summary>The full path of the inspected drawing.</summary>
    public string DrawingPath { get; init; } = string.Empty;

    /// <summary>The DWG file format version, for example <c>AC1032</c>.</summary>
    public string DrawingVersion { get; init; } = string.Empty;

    /// <summary>The host Civil 3D version, for example <c>25.0</c>.</summary>
    public string Civil3DVersion { get; init; } = string.Empty;

    /// <summary>The lightweight drawing statistics (layer, block, xref, entity and table counts).</summary>
    public DrawingStatistics Statistics { get; init; } = new();

    /// <summary>The severity and rule roll-up of the findings.</summary>
    public ValidationSummary Summary { get; init; } = new();

    /// <summary>Per-category severity roll-ups.</summary>
    public IReadOnlyList<ValidationCategory> Categories { get; init; } = Array.Empty<ValidationCategory>();

    /// <summary>The findings, ordered by severity then code.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>Top-level recommendations summarising the state of the drawing.</summary>
    public IReadOnlyList<ValidationRecommendation> Recommendations { get; init; } = Array.Empty<ValidationRecommendation>();

    /// <summary>Timing and step accounting for the workflow run.</summary>
    public ValidationExecutionSummary Execution { get; init; } = new();
}
