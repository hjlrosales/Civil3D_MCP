using System.Text.Json;
using Autodesk.Mcp.Shared.Serialization;
using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Quantity.Analysis;
using Civil3D.Tools.Quantity.Dtos;
using Xunit;
using static Civil3D.Tools.Quantity.Tests.TestDoubles;

namespace Civil3D.Tools.Quantity.Tests;

/// <summary>
/// The pure calculation engine: per-item quantities, per-category summaries, aggregate
/// statistics, empty-data behaviour and report serialization round-trip.
/// </summary>
public class QuantityCalculatorTests
{
    private static QuantityData SampleData() => new()
    {
        Drawing = SampleData_Drawing(),
        Statistics = SampleData_Statistics(),
        Alignments = SampleData_Alignments(),
        Profiles = SampleData_Profiles(),
        Surfaces = SampleData_Surfaces(),
        Corridors = SampleData_Corridors(),
        PipeNetworks = SampleData_PipeNetworks(),
        CogoPoints = SampleData_CogoPoints(),
        Styles = SampleData_Styles(),
    };

    private static ActiveDrawing SampleData_Drawing() => TestDoubles.SampleData.Drawing();
    private static DrawingStatistics SampleData_Statistics() => TestDoubles.SampleData.Statistics();
    private static IReadOnlyList<AlignmentInfo> SampleData_Alignments() => TestDoubles.SampleData.Alignments();
    private static IReadOnlyList<ProfileInfo> SampleData_Profiles() => TestDoubles.SampleData.Profiles();
    private static IReadOnlyList<SurfaceInfo> SampleData_Surfaces() => TestDoubles.SampleData.Surfaces();
    private static IReadOnlyList<CorridorInfo> SampleData_Corridors() => TestDoubles.SampleData.Corridors();
    private static IReadOnlyList<PipeNetworkInfo> SampleData_PipeNetworks() => TestDoubles.SampleData.PipeNetworks();
    private static IReadOnlyList<CogoPointInfo> SampleData_CogoPoints() => TestDoubles.SampleData.CogoPoints();
    private static IReadOnlyList<StyleInfo> SampleData_Styles() => TestDoubles.SampleData.Styles();

    [Fact]
    public void Calculate_ProducesInventoryItemsPerDiscipline()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());

        Assert.Equal(2, Item(result, "alignment.count").Quantity);
        Assert.Equal(1_500, Item(result, "alignment.total_length").Quantity);
        Assert.Equal(QuantityUnit.Length, Item(result, "alignment.total_length").Unit);
        Assert.Equal(1, Item(result, "profile.count").Quantity);
        Assert.Equal(800, Item(result, "profile.total_length").Quantity);
        Assert.Equal(1, Item(result, "surface.count").Quantity);
        Assert.Equal(42_000, Item(result, "surface.total_points").Quantity);
        Assert.Equal(1, Item(result, "corridor.count").Quantity);
        Assert.Equal(2, Item(result, "corridor.total_baselines").Quantity);
        Assert.Equal(1, Item(result, "corridor.total_surfaces").Quantity);
        Assert.Equal(1, Item(result, "pipe_network.count").Quantity);
        Assert.Equal(1, Item(result, "pipe.count").Quantity);
        Assert.Equal(1, Item(result, "structure.count").Quantity);
        Assert.Equal(3, Item(result, "cogo_point.count").Quantity);
        Assert.Equal(1, Item(result, "cogo_point.locked_count").Quantity);
        Assert.Equal(3, Item(result, "style.count").Quantity);
    }

    [Fact]
    public void Calculate_ProducesDrawingLevelCountsAndSize()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());

        Assert.Equal(12, Item(result, "drawing.layer_count").Quantity);
        Assert.Equal(2, Item(result, "drawing.xref_count").Quantity);
        Assert.Equal(3_400, Item(result, "drawing.entity_count").Quantity);
        Assert.Equal(2_500_000, Item(result, "drawing.approximate_size_bytes").Quantity);
        Assert.Equal(QuantityUnit.Bytes, Item(result, "drawing.approximate_size_bytes").Unit);
    }

    [Fact]
    public void Calculate_ProducesPerCategorySummaries()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());

        QuantitySummary alignments = result.Summaries.Single(s => s.Category == QuantityCategory.Alignments);
        Assert.Equal(2, alignments.ItemCount);
        Assert.Equal(2, alignments.TotalQuantity);
        Assert.Equal("2 objects", alignments.TotalLabel);

        QuantitySummary pipes = result.Summaries.Single(s => s.Category == QuantityCategory.Pipes);
        Assert.Equal(3, pipes.ItemCount);
        Assert.Equal(3, pipes.TotalQuantity);

        Assert.Contains(result.Summaries, s => s.Category == QuantityCategory.Drawing);
    }

    [Fact]
    public void Calculate_AggregateStatistics_RollUpAllDisciplines()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());

        Assert.Equal(12, result.Statistics.TotalDomainObjects);
        Assert.Equal(2_300, result.Statistics.TotalLinearLength);
        Assert.Equal(42_000, result.Statistics.TotalSurfacePoints);
        Assert.Equal(2, result.Statistics.TotalCorridorBaselines);
        Assert.Equal(1, result.Statistics.TotalCorridorSurfaces);
        Assert.Equal(1, result.Statistics.TotalPipes);
        Assert.Equal(1, result.Statistics.TotalStructures);
        Assert.Equal(1, result.Statistics.LockedCogoPointCount);
        Assert.Equal(3_400, result.Statistics.TotalEntities);
        Assert.Equal(2_500_000, result.Statistics.ApproximateDrawingSizeBytes);
    }

    [Fact]
    public void Calculate_Overview_CopiesDrawingIdentity()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());

        Assert.Equal("QuantitySample.dwg", result.Overview.DrawingName);
        Assert.Equal("AC1032", result.Overview.DrawingVersion);
        Assert.Equal("25.0", result.Overview.Civil3DVersion);
        Assert.Equal(1, result.Overview.OpenDocumentsCount);
    }

    [Fact]
    public void Calculate_EmptyData_ProducesZeroQuantityItemsAndNoSummaries()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(new QuantityData { Drawing = TestDoubles.SampleData.Drawing() });

        // Every discipline still reports its (zero) count so the inventory stays complete.
        Assert.Contains(result.Items, i => i.Key == "alignment.count" && i.Quantity == 0);
        Assert.Equal(0, result.Statistics.TotalDomainObjects);
        Assert.Equal(0, result.Statistics.TotalLinearLength);
    }

    [Fact]
    public void Calculate_NullStatistics_OmitsDrawingItems()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(new QuantityData
        {
            Drawing = TestDoubles.SampleData.Drawing(),
            Alignments = SampleData_Alignments(),
        });

        Assert.Contains(result.Items, i => i.Key == "alignment.count");
        Assert.DoesNotContain(result.Items, i => i.Category == QuantityCategory.Drawing);
    }

    [Fact]
    public void Report_SerializesAndRoundTrips()
    {
        QuantityTakeoffResult result = QuantityCalculator.Calculate(SampleData());
        var report = new QuantityTakeoffReport
        {
            Overview = result.Overview,
            Items = result.Items,
            Summaries = result.Summaries,
            Statistics = result.Statistics,
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        QuantityTakeoffReport? roundTrip = JsonSerializer.Deserialize<QuantityTakeoffReport>(json, SharedJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Equal(report.Overview.DrawingName, roundTrip!.Overview.DrawingName);
        Assert.Equal(report.Items.Count, roundTrip.Items.Count);
        Assert.Equal(report.Items[0].Key, roundTrip.Items[0].Key);
        Assert.Equal(report.Items[0].Unit, roundTrip.Items[0].Unit);
        Assert.Equal(report.Summaries[0].Category, roundTrip.Summaries[0].Category);
        Assert.Equal(report.Statistics.TotalLinearLength, roundTrip.Statistics.TotalLinearLength);
    }

    private static QuantityItem Item(QuantityTakeoffResult result, string key)
        => result.Items.Single(i => i.Key == key);
}
