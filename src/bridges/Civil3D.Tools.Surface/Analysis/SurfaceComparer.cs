using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Surface.Dtos;

namespace Civil3D.Tools.Surface.Analysis;

/// <summary>
/// Pure, Autodesk-free comparison engine for the surface comparison report. Compares two
/// <see cref="SurfaceInfo"/> snapshots using only the metrics the domain layer exposes (name,
/// kind, point count, minimum/maximum/mean elevation) and produces the per-metric comparisons,
/// differences, optional numeric statistics and optional recommendations. Triangle counts,
/// boundary counts, extents and build status are not exposed by the current DTOs and are
/// therefore omitted rather than invented. The class holds no state; every method is static, so
/// it is trivially testable.
/// </summary>
public static class SurfaceComparer
{
    /// <summary>Compares the two surfaces in the snapshot.</summary>
    /// <param name="data">The loaded surfaces and thresholds.</param>
    public static SurfaceComparisonResult Compare(SurfaceComparisonData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SurfaceInfo existing = data.ExistingSurface;
        SurfaceInfo proposed = data.ProposedSurface;

        IReadOnlyList<SurfaceMetricComparison> metrics = BuildMetrics(existing, proposed);
        IReadOnlyList<SurfaceDifference> differences = BuildDifferences(existing, proposed, data.Options);
        SurfaceComparisonStatistics? statistics = data.IncludeStatistics
            ? BuildStatistics(existing, proposed)
            : null;
        IReadOnlyList<ComparisonRecommendation> recommendations = data.IncludeRecommendations
            ? BuildRecommendations(existing, proposed, differences, data.Options)
            : Array.Empty<ComparisonRecommendation>();

        int significant = differences.Count(d => d.Severity >= ComparisonSeverity.Warning);
        var summary = new SurfaceComparisonSummary
        {
            ExistingSurfaceId = existing.Id,
            ExistingSurfaceName = existing.Name,
            ProposedSurfaceId = proposed.Id,
            ProposedSurfaceName = proposed.Name,
            MetricCount = metrics.Count,
            DifferenceCount = differences.Count,
            SignificantDifferenceCount = significant,
            RecommendationCount = recommendations.Count,
            Verdict = significant > 0 ? "Review Required" : "Compatible",
        };

        return new SurfaceComparisonResult(summary, metrics, differences, statistics, recommendations);
    }

    private static IReadOnlyList<SurfaceMetricComparison> BuildMetrics(SurfaceInfo existing, SurfaceInfo proposed)
    {
        return
        [
            Metric("name", "Name", existing.Name, proposed.Name, string.Empty,
                !string.Equals(existing.Name, proposed.Name, StringComparison.OrdinalIgnoreCase)),
            Metric("kind", "Type", existing.Kind.ToString(), proposed.Kind.ToString(), string.Empty,
                existing.Kind != proposed.Kind),
            Metric("pointCount", "Point count", existing.PointCount.ToString(), proposed.PointCount.ToString(),
                "points", existing.PointCount != proposed.PointCount),
            Metric("minElevation", "Minimum elevation", existing.MinimumElevation.ToString("0.###"),
                proposed.MinimumElevation.ToString("0.###"), "elevation",
                Math.Abs(existing.MinimumElevation - proposed.MinimumElevation) > 0.0001),
            Metric("maxElevation", "Maximum elevation", existing.MaximumElevation.ToString("0.###"),
                proposed.MaximumElevation.ToString("0.###"), "elevation",
                Math.Abs(existing.MaximumElevation - proposed.MaximumElevation) > 0.0001),
            Metric("meanElevation", "Average elevation", existing.MeanElevation.ToString("0.###"),
                proposed.MeanElevation.ToString("0.###"), "elevation",
                Math.Abs(existing.MeanElevation - proposed.MeanElevation) > 0.0001),
        ];
    }

    private static SurfaceMetricComparison Metric(
        string key, string name, string existing, string proposed, string unit, bool significant)
        => new()
        {
            MetricKey = key,
            MetricName = name,
            ExistingValue = existing,
            ProposedValue = proposed,
            Unit = unit,
            IsSignificant = significant,
        };

    private static IReadOnlyList<SurfaceDifference> BuildDifferences(
        SurfaceInfo existing, SurfaceInfo proposed, SurfaceComparisonOptions options)
    {
        var differences = new List<SurfaceDifference>();

        if (!string.Equals(existing.Name, proposed.Name, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add(new SurfaceDifference
            {
                MetricKey = "name",
                MetricName = "Name",
                Description = $"Surface names differ: '{existing.Name}' vs '{proposed.Name}'.",
                Severity = ComparisonSeverity.Information,
            });
        }

        if (existing.Kind != proposed.Kind)
        {
            differences.Add(new SurfaceDifference
            {
                MetricKey = "kind",
                MetricName = "Type",
                Description = $"Surface types differ: {existing.Kind} vs {proposed.Kind}.",
                Severity = ComparisonSeverity.Warning,
            });
        }

        int pointDelta = proposed.PointCount - existing.PointCount;
        if (pointDelta != 0)
        {
            int larger = Math.Max(existing.PointCount, proposed.PointCount);
            double ratio = larger > 0 ? Math.Abs((double)pointDelta) / larger : 0;
            differences.Add(new SurfaceDifference
            {
                MetricKey = "pointCount",
                MetricName = "Point count",
                Description = $"Point count differs by {Math.Abs(pointDelta)} "
                    + $"({ratio * 100:0.#}% of the larger surface).",
                Severity = ratio >= options.PointCountDifferenceRatio
                    ? ComparisonSeverity.Warning
                    : ComparisonSeverity.Information,
            });
        }

        AddElevationDifference(
            differences, "minElevation", "Minimum elevation",
            existing.MinimumElevation, proposed.MinimumElevation, options);
        AddElevationDifference(
            differences, "maxElevation", "Maximum elevation",
            existing.MaximumElevation, proposed.MaximumElevation, options);
        AddElevationDifference(
            differences, "meanElevation", "Average elevation",
            existing.MeanElevation, proposed.MeanElevation, options);

        return differences
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.MetricKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddElevationDifference(
        List<SurfaceDifference> differences, string key, string name,
        double existing, double proposed, SurfaceComparisonOptions options)
    {
        double delta = proposed - existing;
        if (Math.Abs(delta) <= 0.0001)
        {
            return;
        }

        // Mean elevation is a whole-surface statistic, so it uses the tighter mean tolerance.
        // Single min/max elevation readings are point samples of the range, so they use the
        // (larger) range tolerance to avoid flagging ordinary edge-point variation.
        bool significant = Math.Abs(delta) >= (key == "meanElevation"
            ? options.MeanElevationTolerance
            : options.ElevationRangeTolerance);

        differences.Add(new SurfaceDifference
        {
            MetricKey = key,
            MetricName = name,
            Description = $"{name} is {Math.Abs(delta):0.###} {(delta >= 0 ? "higher" : "lower")} "
                + "on the proposed surface.",
            Severity = significant ? ComparisonSeverity.Warning : ComparisonSeverity.Information,
        });
    }

    private static SurfaceComparisonStatistics BuildStatistics(SurfaceInfo existing, SurfaceInfo proposed)
    {
        int larger = Math.Max(existing.PointCount, proposed.PointCount);
        int pointDelta = proposed.PointCount - existing.PointCount;
        double percent = larger > 0 ? Math.Abs((double)pointDelta) / larger * 100 : 0;

        double existingRange = existing.MaximumElevation - existing.MinimumElevation;
        double proposedRange = proposed.MaximumElevation - proposed.MinimumElevation;

        return new SurfaceComparisonStatistics
        {
            PointCountDelta = pointDelta,
            PointCountDeltaPercent = Math.Round(percent, 2),
            MinElevationDelta = Math.Round(proposed.MinimumElevation - existing.MinimumElevation, 3),
            MaxElevationDelta = Math.Round(proposed.MaximumElevation - existing.MaximumElevation, 3),
            MeanElevationDelta = Math.Round(proposed.MeanElevation - existing.MeanElevation, 3),
            ElevationRangeDelta = Math.Round(proposedRange - existingRange, 3),
        };
    }

    private static IReadOnlyList<ComparisonRecommendation> BuildRecommendations(
        SurfaceInfo existing, SurfaceInfo proposed,
        IReadOnlyList<SurfaceDifference> differences, SurfaceComparisonOptions options)
    {
        var recommendations = new List<ComparisonRecommendation>();

        bool anySignificant = differences.Any(d => d.Severity >= ComparisonSeverity.Warning);

        if (proposed.PointCount < existing.PointCount * options.OutdatedSurfaceRatio)
        {
            recommendations.Add(new ComparisonRecommendation
            {
                Title = "Surface appears outdated",
                Description = $"The proposed surface has {proposed.PointCount} points versus "
                    + $"{existing.PointCount} on the existing surface — below {options.OutdatedSurfaceRatio * 100:0}% "
                    + "of the reference density.",
                Severity = ComparisonSeverity.Warning,
                SuggestedAction = "Verify the proposed surface was built from the latest data.",
                RelatedSurface = proposed.Name,
            });
        }

        int larger = Math.Max(existing.PointCount, proposed.PointCount);
        double pointRatio = larger > 0
            ? Math.Abs((double)(proposed.PointCount - existing.PointCount)) / larger
            : 0;
        if (pointRatio >= options.PointCountDifferenceRatio)
        {
            recommendations.Add(new ComparisonRecommendation
            {
                Title = "Large point-count difference",
                Description = $"Point counts differ by {pointRatio * 100:0.#}% of the larger surface "
                    + $"({existing.PointCount} vs {proposed.PointCount}).",
                Severity = ComparisonSeverity.Warning,
                SuggestedAction = "Confirm both surfaces were built from comparable data sets.",
            });
        }

        double rangeDelta = (proposed.MaximumElevation - proposed.MinimumElevation)
            - (existing.MaximumElevation - existing.MinimumElevation);
        if (Math.Abs(rangeDelta) >= options.ElevationRangeTolerance)
        {
            recommendations.Add(new ComparisonRecommendation
            {
                Title = "Large elevation range difference",
                Description = $"The elevation range differs by {Math.Abs(rangeDelta):0.###} drawing units "
                    + $"({existing.MaximumElevation - existing.MinimumElevation:0.###} vs "
                    + $"{proposed.MaximumElevation - proposed.MinimumElevation:0.###}).",
                Severity = ComparisonSeverity.Warning,
                SuggestedAction = "Check whether the surfaces capture the same terrain extents.",
            });
        }

        double meanDelta = proposed.MeanElevation - existing.MeanElevation;
        if (Math.Abs(meanDelta) >= options.MeanElevationTolerance)
        {
            recommendations.Add(new ComparisonRecommendation
            {
                Title = "Review before volume calculations",
                Description = $"Average elevation differs by {Math.Abs(meanDelta):0.###} drawing units; "
                    + "volume results computed from these surfaces will diverge.",
                Severity = ComparisonSeverity.Warning,
                SuggestedAction = "Reconcile the surfaces before running cut/fill or volume calculations.",
            });
        }

        if (!anySignificant && recommendations.Count == 0)
        {
            recommendations.Add(new ComparisonRecommendation
            {
                Title = "Surfaces are compatible",
                Description = "No significant metric differences were found between the two surfaces.",
                Severity = ComparisonSeverity.Information,
                SuggestedAction = "No action required.",
            });
        }

        return recommendations
            .OrderByDescending(r => r.Severity)
            .ThenBy(r => r.Title, StringComparer.Ordinal)
            .ToArray();
    }
}
