using Civil3D.Domain.Styles.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Quantity.Dtos;

namespace Civil3D.Tools.Quantity.Analysis;

/// <summary>
/// Pure, Autodesk-free calculation engine for the quantity takeoff report. Turns a materialized
/// <see cref="QuantityData"/> snapshot into the drawing overview, per-item quantity lines,
/// per-category roll-ups and aggregate statistics. The class holds no state; every calculation
/// is a static method, so it is trivially testable. Only metrics exposed by the existing domain
/// DTOs are produced — nothing is invented, and no geometry is computed.
/// </summary>
public static class QuantityCalculator
{
    /// <summary>Runs every quantity calculation over the given data in a single pass.</summary>
    /// <param name="data">The materialized drawing and domain data.</param>
    public static QuantityTakeoffResult Calculate(QuantityData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        QuantityOverview overview = BuildOverview(data.Drawing);
        QuantityStatistics statistics = BuildStatistics(data);
        IReadOnlyList<QuantityItem> items = BuildItems(data, statistics);
        IReadOnlyList<QuantitySummary> summaries = BuildSummaries(items);

        return new QuantityTakeoffResult(overview, items, summaries, statistics);
    }

    private static QuantityOverview BuildOverview(ActiveDrawing drawing)
        => new()
        {
            DrawingName = drawing.DrawingName,
            DrawingPath = drawing.DrawingPath,
            DrawingVersion = drawing.DrawingVersion,
            Civil3DVersion = drawing.Civil3DVersion,
            IsModified = drawing.IsModified,
            IsReadOnly = drawing.IsReadOnly,
            IsModelSpaceActive = drawing.IsModelSpaceActive,
            DatabaseFingerprint = drawing.DatabaseFingerprint,
            OpenDocumentsCount = drawing.OpenDocumentsCount,
        };

    private static QuantityStatistics BuildStatistics(QuantityData data)
    {
        int pipes = data.PipeNetworks.Sum(n => n.PipeCount);
        int structures = data.PipeNetworks.Sum(n => n.StructureCount);
        DrawingStatistics? stats = data.Statistics;

        return new QuantityStatistics
        {
            TotalDomainObjects = data.Alignments.Count + data.Profiles.Count + data.Surfaces.Count
                + data.Corridors.Count + data.PipeNetworks.Count + data.CogoPoints.Count
                + data.Styles.Count,
            TotalLinearLength = data.Alignments.Sum(a => a.Length) + data.Profiles.Sum(p => p.Length),
            TotalSurfacePoints = data.Surfaces.Sum(s => s.PointCount),
            TotalCorridorBaselines = data.Corridors.Sum(c => c.BaselineCount),
            TotalCorridorSurfaces = data.Corridors.Sum(c => c.CorridorSurfaceCount),
            TotalPipes = pipes,
            TotalStructures = structures,
            LockedCogoPointCount = data.CogoPoints.Count(p => p.IsLocked),
            TotalEntities = stats?.EntityCount ?? 0,
            ApproximateDrawingSizeBytes = stats?.ApproximateDrawingSizeBytes ?? 0,
        };
    }

    private static IReadOnlyList<QuantityItem> BuildItems(QuantityData data, QuantityStatistics stats)
    {
        var items = new List<QuantityItem>();

        // Alignments.
        items.Add(CountItem(QuantityCategory.Alignments, "alignment.count", "Alignments", data.Alignments.Count));
        items.Add(new QuantityItem
        {
            Category = QuantityCategory.Alignments,
            Key = "alignment.total_length",
            Label = "Total alignment length",
            Quantity = Math.Round(data.Alignments.Sum(a => a.Length), 3),
            Unit = QuantityUnit.Length,
        });

        // Profiles.
        items.Add(CountItem(QuantityCategory.Profiles, "profile.count", "Profiles", data.Profiles.Count));
        items.Add(new QuantityItem
        {
            Category = QuantityCategory.Profiles,
            Key = "profile.total_length",
            Label = "Total profile length",
            Quantity = Math.Round(data.Profiles.Sum(p => p.Length), 3),
            Unit = QuantityUnit.Length,
        });

        // Surfaces.
        items.Add(CountItem(QuantityCategory.Surfaces, "surface.count", "Surfaces", data.Surfaces.Count));
        items.Add(new QuantityItem
        {
            Category = QuantityCategory.Surfaces,
            Key = "surface.total_points",
            Label = "Total surface definition points",
            Quantity = stats.TotalSurfacePoints,
            Unit = QuantityUnit.Count,
        });

        // Corridors.
        items.Add(CountItem(QuantityCategory.Corridors, "corridor.count", "Corridors", data.Corridors.Count));
        items.Add(CountItem(QuantityCategory.Corridors, "corridor.total_baselines", "Total corridor baselines", stats.TotalCorridorBaselines));
        items.Add(CountItem(QuantityCategory.Corridors, "corridor.total_surfaces", "Total corridor surfaces", stats.TotalCorridorSurfaces));

        // Pipe networks.
        items.Add(CountItem(QuantityCategory.Pipes, "pipe_network.count", "Pipe networks", data.PipeNetworks.Count));
        items.Add(CountItem(QuantityCategory.Pipes, "pipe.count", "Pipes", stats.TotalPipes));
        items.Add(CountItem(QuantityCategory.Pipes, "structure.count", "Structures", stats.TotalStructures));

        // COGO points.
        items.Add(CountItem(QuantityCategory.CogoPoints, "cogo_point.count", "COGO points", data.CogoPoints.Count));
        items.Add(CountItem(QuantityCategory.CogoPoints, "cogo_point.locked_count", "Locked COGO points", stats.LockedCogoPointCount));

        // Styles, broken down by kind.
        items.Add(CountItem(QuantityCategory.Styles, "style.count", "Styles", data.Styles.Count));
        foreach (StyleKind kind in Enum.GetValues<StyleKind>())
        {
            int count = data.Styles.Count(s => s.Kind == kind);
            if (count > 0)
            {
                items.Add(CountItem(
                    QuantityCategory.Styles, $"style.count.{kind.ToString().ToLowerInvariant()}",
                    $"{kind} styles", count));
            }
        }

        // Drawing-level counts and the approximate file size.
        DrawingStatistics? s = data.Statistics;
        if (s is not null)
        {
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.layer_count", "Layers", s.LayerCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.block_count", "Blocks", s.BlockCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.xref_count", "External references", s.XRefCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.entity_count", "Entities", s.EntityCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.model_space_entity_count", "Model space entities", s.ModelSpaceEntityCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.paper_space_entity_count", "Paper space entities", s.PaperSpaceEntityCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.viewport_count", "Viewports", s.ViewportCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.text_style_count", "Text styles", s.TextStyleCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.dimension_style_count", "Dimension styles", s.DimensionStyleCount));
            items.Add(CountItem(QuantityCategory.Drawing, "drawing.linetype_count", "Linetypes", s.LinetypeCount));
            items.Add(new QuantityItem
            {
                Category = QuantityCategory.Drawing,
                Key = "drawing.approximate_size_bytes",
                Label = "Approximate drawing size",
                Quantity = s.ApproximateDrawingSizeBytes,
                Unit = QuantityUnit.Bytes,
            });
        }

        return items
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<QuantitySummary> BuildSummaries(IReadOnlyList<QuantityItem> items)
    {
        var summaries = new List<QuantitySummary>();

        foreach (QuantityCategory category in Enum.GetValues<QuantityCategory>())
        {
            IReadOnlyList<QuantityItem> categoryItems = items.Where(i => i.Category == category).ToArray();
            if (categoryItems.Count == 0)
            {
                continue;
            }

            // Only count-unit items roll into the category total; measured lengths and file
            // sizes are reported in their own lines so the aggregate stays meaningful.
            double total = categoryItems
                .Where(i => i.Unit == QuantityUnit.Count)
                .Sum(i => i.Quantity);

            summaries.Add(new QuantitySummary
            {
                Category = category,
                ItemCount = categoryItems.Count,
                TotalQuantity = total,
                TotalLabel = $"{total:0.##} object{(total == 1 ? string.Empty : "s")}",
            });
        }

        return summaries.OrderBy(s => s.Category).ToArray();
    }

    private static QuantityItem CountItem(
        QuantityCategory category, string key, string label, double quantity)
        => new()
        {
            Category = category,
            Key = key,
            Label = label,
            Quantity = quantity,
            Unit = QuantityUnit.Count,
        };
}
