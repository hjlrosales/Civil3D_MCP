namespace Civil3D.Tools.Validation.Dtos;

/// <summary>
/// Timing and step accounting for the workflow run that produced the report.
/// </summary>
public sealed record ValidationExecutionSummary
{
    /// <summary>The workflow name, <c>design.validation.report</c>.</summary>
    public string WorkflowName { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the workflow started.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>UTC timestamp when the report was generated.</summary>
    public DateTimeOffset FinishedAtUtc { get; init; }

    /// <summary>Total execution duration.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>The total number of workflow steps.</summary>
    public int TotalSteps { get; init; }

    /// <summary>The number of steps that completed.</summary>
    public int CompletedSteps { get; init; }
}
