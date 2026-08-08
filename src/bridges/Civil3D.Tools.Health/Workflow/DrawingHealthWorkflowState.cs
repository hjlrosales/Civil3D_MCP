using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Health.Analysis;
using Civil3D.Tools.Health.Dtos;

namespace Civil3D.Tools.Health.Workflow;

/// <summary>
/// Mutable working state shared between the steps of one drawing-health workflow execution. A
/// fresh instance is created with every workflow; it is never reused across runs, so the mutable
/// collections are safe. Steps write materialized DTOs here; the report step composes them.
/// </summary>
public sealed class DrawingHealthWorkflowState
{
    /// <summary>The active drawing snapshot collected by the drawing step.</summary>
    public ActiveDrawing? Drawing { get; set; }

    /// <summary>The lightweight drawing statistics collected by the drawing step.</summary>
    public DrawingStatistics? Statistics { get; set; }

    /// <summary>All alignments in the drawing.</summary>
    public IReadOnlyList<AlignmentInfo> Alignments { get; set; } = Array.Empty<AlignmentInfo>();

    /// <summary>All surfaces in the drawing.</summary>
    public IReadOnlyList<SurfaceInfo> Surfaces { get; set; } = Array.Empty<SurfaceInfo>();

    /// <summary>All profiles in the drawing.</summary>
    public IReadOnlyList<ProfileInfo> Profiles { get; set; } = Array.Empty<ProfileInfo>();

    /// <summary>All corridors in the drawing.</summary>
    public IReadOnlyList<CorridorInfo> Corridors { get; set; } = Array.Empty<CorridorInfo>();

    /// <summary>All pipe networks in the drawing.</summary>
    public IReadOnlyList<PipeNetworkInfo> PipeNetworks { get; set; } = Array.Empty<PipeNetworkInfo>();

    /// <summary>All COGO points in the drawing.</summary>
    public IReadOnlyList<CogoPointInfo> CogoPoints { get; set; } = Array.Empty<CogoPointInfo>();

    /// <summary>All styles in the drawing.</summary>
    public IReadOnlyList<StyleInfo> Styles { get; set; } = Array.Empty<StyleInfo>();

    /// <summary>The analyzer output produced by the analysis step.</summary>
    public HealthAnalysisResult? Analysis { get; set; }

    /// <summary>The final report produced by the report step.</summary>
    public DrawingHealthReport? Report { get; set; }
}
