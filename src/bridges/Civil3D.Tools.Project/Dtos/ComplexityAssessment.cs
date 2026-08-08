namespace Civil3D.Tools.Project.Dtos;

/// <summary>
/// The complexity classification of the drawing with its numeric score and a short reason.
/// </summary>
public sealed record ComplexityAssessment
{
    /// <summary>The complexity classification (Small, Medium, Large, Enterprise).</summary>
    public ProjectComplexity Classification { get; init; }

    /// <summary>The numeric complexity score; thresholds come from the analyzer options.</summary>
    public double Score { get; init; }

    /// <summary>A human-readable explanation of the classification.</summary>
    public string Reason { get; init; } = string.Empty;
}
