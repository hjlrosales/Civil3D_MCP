using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Project.Analysis;

/// <summary>
/// Immutable snapshot of everything the project analyzer inspects: the active drawing, the
/// lightweight drawing statistics and the materialized domain collections. Produced by the
/// workflow's collection steps and consumed by <see cref="ProjectAnalyzer"/>. Autodesk-free.
/// </summary>
public sealed record ProjectData
{
    /// <summary>The active drawing snapshot.</summary>
    public ActiveDrawing Drawing { get; init; } = new();

    /// <summary>The lightweight drawing statistics; null when unavailable.</summary>
    public DrawingStatistics? Statistics { get; init; }

    /// <summary>All alignments in the drawing.</summary>
    public IReadOnlyList<AlignmentInfo> Alignments { get; init; } = Array.Empty<AlignmentInfo>();

    /// <summary>All surfaces in the drawing.</summary>
    public IReadOnlyList<SurfaceInfo> Surfaces { get; init; } = Array.Empty<SurfaceInfo>();

    /// <summary>All profiles in the drawing.</summary>
    public IReadOnlyList<ProfileInfo> Profiles { get; init; } = Array.Empty<ProfileInfo>();

    /// <summary>All corridors in the drawing.</summary>
    public IReadOnlyList<CorridorInfo> Corridors { get; init; } = Array.Empty<CorridorInfo>();

    /// <summary>All pipe networks in the drawing.</summary>
    public IReadOnlyList<PipeNetworkInfo> PipeNetworks { get; init; } = Array.Empty<PipeNetworkInfo>();

    /// <summary>All COGO points in the drawing.</summary>
    public IReadOnlyList<CogoPointInfo> CogoPoints { get; init; } = Array.Empty<CogoPointInfo>();

    /// <summary>All styles in the drawing.</summary>
    public IReadOnlyList<StyleInfo> Styles { get; init; } = Array.Empty<StyleInfo>();

    /// <summary>The total number of domain objects inspected.</summary>
    public int ObjectCount =>
        Alignments.Count + Surfaces.Count + Profiles.Count + Corridors.Count +
        PipeNetworks.Count + CogoPoints.Count + Styles.Count;
}
