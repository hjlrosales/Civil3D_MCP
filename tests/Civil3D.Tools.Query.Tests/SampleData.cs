using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query.Tests;

/// <summary>
/// Canned in-memory sample data for every discipline. Query tool tests exercise the real
/// <see cref="Civil3D.Domain.Query.QueryEngine"/> through these items, exactly as production
/// services do.
/// </summary>
internal static class SampleData
{
    internal static readonly ActiveDrawing Drawing = new()
    {
        DrawingName = "QuerySample.dwg",
        DrawingPath = @"C:\Drawings\QuerySample.dwg",
        DrawingVersion = "AC1032",
        IsModified = false,
        IsReadOnly = false,
        CurrentLayout = "Model",
        IsModelSpaceActive = true,
        DatabaseFingerprint = "fp-query",
        Civil3DVersion = "25.0",
        OpenDocumentsCount = 1,
        CurrentDocumentName = "QuerySample.dwg",
        CurrentDocumentPath = @"C:\Drawings\QuerySample.dwg",
    };

    internal static IReadOnlyList<AlignmentInfo> Alignments =>
    [
        new()
        {
            Id = 1,
            Name = "Mainline",
            Description = "Primary road corridor",
            Length = 1_000,
            StartingStation = 0,
            EndingStation = 1_000,
            StyleId = 100,
        },
        new()
        {
            Id = 2,
            Name = "Ramp A",
            Description = "Curved ramp",
            Length = 300,
            StartingStation = 0,
            EndingStation = 300,
        },
    ];

    internal static IReadOnlyList<SurfaceInfo> Surfaces =>
    [
        new() { Id = 1, Name = "EG Surface", Description = "Existing ground", PointCount = 5_000 },
        new() { Id = 2, Name = "FG Surface", Description = "Finished grade", PointCount = 7_000 },
    ];

    internal static IReadOnlyList<ProfileInfo> Profiles =>
    [
        new()
        {
            Id = 1,
            Name = "CL Profile",
            Description = "Centerline layout profile",
            AlignmentId = 1,
            TypeName = "Layout",
            Length = 1_000,
            StartingStation = 0,
            EndingStation = 1_000,
        },
        new()
        {
            Id = 2,
            Name = "EG Profile",
            Description = "Existing ground profile",
            AlignmentId = 1,
            TypeName = "ExistingGround",
            Length = 1_000,
            StartingStation = 0,
            EndingStation = 1_000,
        },
    ];

    internal static IReadOnlyList<CorridorInfo> Corridors =>
    [
        new()
        {
            Id = 1,
            Name = "Main Corridor",
            Description = "Mainline corridor",
            StyleId = 100,
            CodeSetStyleId = 200,
            AlignmentId = 1,
            BaselineCount = 2,
            CorridorSurfaceCount = 1,
        },
    ];

    internal static IReadOnlyList<PipeNetworkInfo> PipeNetworks =>
    [
        new() { Id = 1, Name = "Storm Network", Description = "Storm drainage network", PartsListName = "Standard" },
    ];

    internal static IReadOnlyList<CogoPointInfo> CogoPoints =>
    [
        new()
        {
            Id = 1,
            PointNumber = 101,
            Easting = 100.5,
            Northing = 200.5,
            Elevation = 35.2,
            FullDescription = "Benchmark",
            IsLocked = true,
        },
        new()
        {
            Id = 2,
            PointNumber = 102,
            Easting = 150.0,
            Northing = 250.0,
            Elevation = 36.1,
            FullDescription = "Road center",
            IsLocked = false,
        },
    ];

    internal static IReadOnlyList<StyleInfo> Styles =>
    [
        new() { Id = 100, Name = "Road Style", Description = "Road alignment style" },
        new() { Id = 101, Name = "Surface Style", Description = "Surface display style" },
        new() { Id = 200, Name = "Code Set", Description = "Corridor code set style" },
    ];
}
