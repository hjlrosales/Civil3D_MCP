using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Dtos;

namespace Civil3D.Tools.CutFill.Analysis;

/// <summary>
/// Pure, Autodesk-free analysis engine for the cut/fill report. Turns the raw calculator output
/// into the report pieces: the volume summary with verdict, contextual surface differences,
/// optional derived statistics and optional recommendations derived only from calculated values.
/// The class holds no state; every method is static, so it is trivially testable.
/// </summary>
public static class CutFillAnalyzer
{
    /// <summary>Analyzes the calculator output for the report.</summary>
    /// <param name="data">The loaded surfaces and thresholds.</param>
    /// <param name="result">The calculator output.</param>
    /// <param name="includeStatistics">When true, derive the statistics section.</param>
    /// <param name="includeRecommendations">When true, derive recommendations.</param>
    public static CutFillAnalysisResult Analyze(
        CutFillCalculationData data, CutFillCalculationResult result,
        bool includeStatistics = true, bool includeRecommendations = true)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(result);

        SurfaceInfo existing = data.ExistingSurface;
        SurfaceInfo proposed = data.ProposedSurface;
        IReadOnlyList<VolumeDifference> differences = BuildDifferences(existing, proposed);

        if (result.Status == CutFillStatus.NotSupported)
        {
            return new CutFillAnalysisResult(
                new VolumeSummary
                {
                    ExistingSurfaceId = existing.Id,
                    ExistingSurfaceName = existing.Name,
                    ProposedSurfaceId = proposed.Id,
                    ProposedSurfaceName = proposed.Name,
                    Status = CutFillStatus.NotSupported,
                    NotSupportedReason = result.NotSupportedReason,
                    Verdict = "Not Supported",
                },
                differences,
                Statistics: null,
                Recommendations: Array.Empty<CutFillRecommendation>());
        }

        // Volumes are clamped to non-negative magnitudes; the signed net is derived from the
        // clamped values so the summary is always internally consistent (net = cut − fill).
        double cut = Math.Max(0, result.CutVolume);
        double fill = Math.Max(0, result.FillVolume);
        double net = cut - fill;
        double total = cut + fill;
        double netRatio = total > 0 ? Math.Abs(net) / total : 0;

        string verdict = ClassifyVerdict(net, total, netRatio, data.Options.BalanceThreshold);

        return new CutFillAnalysisResult(
            new VolumeSummary
            {
                ExistingSurfaceId = existing.Id,
                ExistingSurfaceName = existing.Name,
                ProposedSurfaceId = proposed.Id,
                ProposedSurfaceName = proposed.Name,
                Status = CutFillStatus.Computed,
                CutVolume = cut,
                FillVolume = fill,
                NetVolume = net,
                SurfaceAreaUsed = result.SurfaceAreaUsed,
                Verdict = verdict,
                IsBalanced = netRatio <= data.Options.BalanceThreshold,
            },
            differences,
            includeStatistics ? BuildStatistics(cut, fill, net, total) : null,
            includeRecommendations
                ? BuildRecommendations(existing, proposed, cut, fill, net, total, netRatio, data.Options)
                : Array.Empty<CutFillRecommendation>());
    }

    private static string ClassifyVerdict(double net, double total, double netRatio, double balanceThreshold)
    {
        if (total <= 0)
        {
            return "No Earthwork";
        }

        if (netRatio <= balanceThreshold)
        {
            return "Balanced Earthwork";
        }

        return net >= 0 ? "Predominantly Cut" : "Predominantly Fill";
    }

    private static VolumeStatistics? BuildStatistics(double cut, double fill, double net, double total)
    {
        if (total <= 0)
        {
            return null;
        }

        return new VolumeStatistics
        {
            CutPercentOfTotal = Math.Round(cut / total * 100, 2),
            FillPercentOfTotal = Math.Round(fill / total * 100, 2),
            NetPercentOfTotal = Math.Round(net / total * 100, 2),
            CutFillRatio = fill > 0 ? Math.Round(cut / fill, 3) : 0,
        };
    }

    private static IReadOnlyList<CutFillRecommendation> BuildRecommendations(
        SurfaceInfo existing, SurfaceInfo proposed,
        double cut, double fill, double net, double total, double netRatio,
        CutFillOptions options)
    {
        var recommendations = new List<CutFillRecommendation>();

        if (total <= 0)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "No earthwork required",
                Description = "Both cut and fill volumes are zero; the surfaces coincide.",
                Severity = CutFillSeverity.Information,
                SuggestedAction = "No action required.",
            });
            return recommendations;
        }

        if (netRatio <= options.BalanceThreshold)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Balanced earthwork",
                Description = $"Net volume is {Math.Abs(net):0.###} ({netRatio * 100:0.#}% of the total "
                    + $"{total:0.###}), within the {options.BalanceThreshold * 100:0}% balance threshold.",
                Severity = CutFillSeverity.Information,
                SuggestedAction = "No haulage management required.",
            });
        }
        else if (net >= 0)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Predominantly cut",
                Description = $"Cut volume {cut:0.###} exceeds fill volume {fill:0.###} by "
                    + $"{net:0.###} cubic units.",
                Severity = CutFillSeverity.Warning,
                SuggestedAction = "Plan for disposal or reuse of the surplus excavated material.",
            });
        }
        else
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Predominantly fill",
                Description = $"Fill volume {fill:0.###} exceeds cut volume {cut:0.###} by "
                    + $"{Math.Abs(net):0.###} cubic units.",
                Severity = CutFillSeverity.Warning,
                SuggestedAction = "Plan for imported borrow material to satisfy the fill demand.",
            });
        }

        if (net > 0 && netRatio >= options.SignificantImbalanceRatio)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Significant net export",
                Description = $"Net cut of {net:0.###} is {netRatio * 100:0.#}% of the total volume; "
                    + "a significant volume of material must be exported.",
                Severity = CutFillSeverity.Warning,
                SuggestedAction = "Confirm disposal strategy and haulage routes before construction.",
            });
        }
        else if (net < 0 && netRatio >= options.SignificantImbalanceRatio)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Significant net import",
                Description = $"Net fill of {Math.Abs(net):0.###} is {netRatio * 100:0.#}% of the total "
                    + "volume; a significant volume of material must be imported.",
                Severity = CutFillSeverity.Warning,
                SuggestedAction = "Confirm borrow-source availability before construction.",
            });
        }

        int larger = Math.Max(existing.PointCount, proposed.PointCount);
        double pointRatio = larger > 0
            ? Math.Abs((double)(proposed.PointCount - existing.PointCount)) / larger
            : 0;
        if (pointRatio >= options.SurfaceQualityPointRatio)
        {
            recommendations.Add(new CutFillRecommendation
            {
                Title = "Verify surface quality before construction",
                Description = $"Surface point counts differ by {pointRatio * 100:0.#}% of the larger "
                    + $"surface ({existing.PointCount} vs {proposed.PointCount}); volumes may be "
                    + "sensitive to resolution differences.",
                Severity = CutFillSeverity.Warning,
                SuggestedAction = "Verify both surfaces were built from comparable, current data.",
            });
        }

        return recommendations
            .OrderByDescending(r => r.Severity)
            .ThenBy(r => r.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<VolumeDifference> BuildDifferences(SurfaceInfo existing, SurfaceInfo proposed)
    {
        int pointDelta = proposed.PointCount - existing.PointCount;
        double minDelta = proposed.MinimumElevation - existing.MinimumElevation;
        double maxDelta = proposed.MaximumElevation - existing.MaximumElevation;
        double meanDelta = proposed.MeanElevation - existing.MeanElevation;

        return
        [
            Difference("pointCount", "Point count",
                existing.PointCount.ToString(), proposed.PointCount.ToString(),
                pointDelta == 0
                    ? "Point counts are identical."
                    : $"Point count differs by {Math.Abs(pointDelta)} ({(pointDelta > 0 ? "more" : "fewer")} on the proposed surface)."),
            Difference("minElevation", "Minimum elevation",
                existing.MinimumElevation.ToString("0.###"), proposed.MinimumElevation.ToString("0.###"),
                ElevationText("Minimum elevation", minDelta)),
            Difference("maxElevation", "Maximum elevation",
                existing.MaximumElevation.ToString("0.###"), proposed.MaximumElevation.ToString("0.###"),
                ElevationText("Maximum elevation", maxDelta)),
            Difference("meanElevation", "Average elevation",
                existing.MeanElevation.ToString("0.###"), proposed.MeanElevation.ToString("0.###"),
                ElevationText("Average elevation", meanDelta)),
        ];
    }

    private static VolumeDifference Difference(
        string key, string name, string existing, string proposed, string description)
        => new()
        {
            MetricKey = key,
            MetricName = name,
            ExistingValue = existing,
            ProposedValue = proposed,
            Description = description,
        };

    private static string ElevationText(string name, double delta)
        => Math.Abs(delta) <= 0.0001
            ? $"{name} is identical."
            : $"{name} is {Math.Abs(delta):0.###} {(delta >= 0 ? "higher" : "lower")} on the proposed surface.";
}
