using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Tools.Corridor.Dtos;

namespace Civil3D.Tools.Corridor.Analysis;

/// <summary>
/// Pure, Autodesk-free analysis engine for the corridor analysis report. Analyzes
/// <see cref="CorridorInfo"/> snapshots using only the metrics the domain layer exposes (name,
/// description, style ids, primary alignment id, baseline count, corridor-surface count) and
/// produces per-corridor summaries with health status, aggregate statistics, health issues and
/// recommendations. Region counts, assembly usage, target mappings, rebuild status, corridor
/// length, surface-generation status and frequency settings are not exposed by the current
/// DTOs and are therefore omitted rather than invented. The class holds no state; every method
/// is static, so it is trivially testable.
/// </summary>
public static class CorridorAnalyzer
{
    /// <summary>Analyzes the corridors and produces the summaries, issues and statistics.</summary>
    /// <param name="corridors">The corridors to analyze (already filtered to the requested scope).</param>
    /// <param name="options">The thresholds to apply; defaults when omitted.</param>
    /// <param name="includeStatistics">When true, derive the aggregate statistics.</param>
    public static CorridorAnalysisResult Analyze(
        IReadOnlyList<CorridorInfo> corridors,
        CorridorOptions? options = null,
        bool includeStatistics = true)
    {
        ArgumentNullException.ThrowIfNull(corridors);

        var actualOptions = options ?? CorridorOptions.Default;
        var summaries = new List<CorridorSummary>(corridors.Count);
        var issues = new List<CorridorIssue>();

        foreach (CorridorInfo corridor in corridors)
        {
            summaries.Add(BuildSummary(corridor));
            issues.AddRange(BuildIssues(corridor));
        }

        string verdict = ClassifyVerdict(issues, summaries.Count);

        return new CorridorAnalysisResult
        {
            Verdict = verdict,
            Corridors = summaries,
            Statistics = includeStatistics ? BuildStatistics(summaries) : null,
            Issues = issues
                .OrderByDescending(i => i.Severity)
                .ThenBy(i => i.CorridorName, StringComparer.Ordinal)
                .ThenBy(i => i.Code, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    /// <summary>
    /// Builds the recommendations from the available metrics. This is a separate stage so the
    /// workflow can publish distinct progress milestones; the engine stays pure.
    /// </summary>
    /// <param name="corridors">The analyzed corridors.</param>
    /// <param name="options">The thresholds to apply; defaults when omitted.</param>
    public static IReadOnlyList<CorridorRecommendation> BuildRecommendations(
        IReadOnlyList<CorridorInfo> corridors,
        CorridorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(corridors);

        var actualOptions = options ?? CorridorOptions.Default;
        var recommendations = new List<CorridorRecommendation>();

        if (corridors.Count == 0)
        {
            recommendations.Add(new CorridorRecommendation
            {
                Title = "No corridors in the drawing",
                Description = "The drawing contains no corridors to analyze.",
                Severity = CorridorSeverity.Information,
                SuggestedAction = "Create or import a corridor before requesting an analysis.",
            });
            return recommendations;
        }

        foreach (CorridorInfo corridor in corridors)
        {
            if (corridor.CorridorSurfaceCount == 0)
            {
                recommendations.Add(new CorridorRecommendation
                {
                    Title = "Review generated surfaces",
                    Description = $"'{corridor.Name}' has no corridor surfaces; downstream surfaces "
                        + "and volumes may be missing.",
                    Severity = CorridorSeverity.Warning,
                    SuggestedAction = "Generate corridor surfaces before using the corridor for design.",
                    RelatedCorridor = corridor.Name,
                });
            }

            if (corridor.StyleId is null || corridor.CodeSetStyleId is null)
            {
                recommendations.Add(new CorridorRecommendation
                {
                    Title = "Review style assignments",
                    Description = $"'{corridor.Name}' is missing a corridor and/or code set style; "
                        + "rendering may fall back to defaults.",
                    Severity = CorridorSeverity.Warning,
                    SuggestedAction = "Assign the corridor style and code set style.",
                    RelatedCorridor = corridor.Name,
                });
            }

            if (corridor.BaselineCount >= actualOptions.LargeComplexityBaselineThreshold
                || corridor.CorridorSurfaceCount >= actualOptions.LargeComplexitySurfaceThreshold)
            {
                recommendations.Add(new CorridorRecommendation
                {
                    Title = "Large corridor complexity",
                    Description = $"'{corridor.Name}' has {corridor.BaselineCount} baseline(s) and "
                        + $"{corridor.CorridorSurfaceCount} corridor surface(s); rebuilds and updates "
                        + "may be slow.",
                    Severity = CorridorSeverity.Information,
                    SuggestedAction = "Monitor rebuild performance; consider simplifying where possible.",
                    RelatedCorridor = corridor.Name,
                });
            }

            if (corridor.BaselineCount > 0 && corridor.CorridorSurfaceCount > 0)
            {
                recommendations.Add(new CorridorRecommendation
                {
                    Title = "Suitable for quantity takeoff",
                    Description = $"'{corridor.Name}' has baselines and corridor surfaces, so "
                        + "quantity takeoff can run against it.",
                    Severity = CorridorSeverity.Information,
                    SuggestedAction = "Run quantity_takeoff_report for a structured object inventory.",
                    RelatedCorridor = corridor.Name,
                });
            }
        }

        return recommendations
            .OrderByDescending(r => r.Severity)
            .ThenBy(r => r.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static CorridorSummary BuildSummary(CorridorInfo corridor)
    {
        string status = corridor.BaselineCount == 0
            ? "No Baselines"
            : corridor.CorridorSurfaceCount == 0
                ? "No Surfaces"
                : corridor.StyleId is null || corridor.CodeSetStyleId is null
                    ? "Needs Review"
                    : "Healthy";

        return new CorridorSummary
        {
            Id = corridor.Id,
            Name = corridor.Name,
            Description = corridor.Description,
            AlignmentId = corridor.AlignmentId,
            StyleId = corridor.StyleId,
            CodeSetStyleId = corridor.CodeSetStyleId,
            BaselineCount = corridor.BaselineCount,
            CorridorSurfaceCount = corridor.CorridorSurfaceCount,
            Status = status,
        };
    }

    private static IReadOnlyList<CorridorIssue> BuildIssues(CorridorInfo corridor)
    {
        var issues = new List<CorridorIssue>();

        if (corridor.BaselineCount == 0)
        {
            issues.Add(new CorridorIssue
            {
                CorridorId = corridor.Id,
                CorridorName = corridor.Name,
                Code = "noBaselines",
                Title = "No baselines defined",
                Description = $"'{corridor.Name}' has no baselines, so it models no alignment geometry.",
                Severity = CorridorSeverity.Error,
            });
        }

        if (corridor.CorridorSurfaceCount == 0)
        {
            issues.Add(new CorridorIssue
            {
                CorridorId = corridor.Id,
                CorridorName = corridor.Name,
                Code = "noSurfaces",
                Title = "No corridor surfaces generated",
                Description = $"'{corridor.Name}' has no corridor surfaces; design surfaces and "
                    + "volumes are unavailable.",
                Severity = CorridorSeverity.Warning,
            });
        }

        if (corridor.StyleId is null)
        {
            issues.Add(new CorridorIssue
            {
                CorridorId = corridor.Id,
                CorridorName = corridor.Name,
                Code = "missingStyle",
                Title = "Missing corridor style",
                Description = $"'{corridor.Name}' has no corridor style assigned.",
                Severity = CorridorSeverity.Warning,
            });
        }

        if (corridor.CodeSetStyleId is null)
        {
            issues.Add(new CorridorIssue
            {
                CorridorId = corridor.Id,
                CorridorName = corridor.Name,
                Code = "missingCodeSetStyle",
                Title = "Missing code set style",
                Description = $"'{corridor.Name}' has no code set style assigned.",
                Severity = CorridorSeverity.Information,
            });
        }

        if (string.IsNullOrWhiteSpace(corridor.Description))
        {
            issues.Add(new CorridorIssue
            {
                CorridorId = corridor.Id,
                CorridorName = corridor.Name,
                Code = "missingDescription",
                Title = "Missing description",
                Description = $"'{corridor.Name}' has no description; add one for project context.",
                Severity = CorridorSeverity.Information,
            });
        }

        return issues;
    }

    private static string ClassifyVerdict(IReadOnlyList<CorridorIssue> issues, int corridorCount)
    {
        if (corridorCount == 0)
        {
            return "No Corridors";
        }

        if (issues.Any(i => i.Severity >= CorridorSeverity.Error))
        {
            return "Attention Required";
        }

        return issues.Any(i => i.Severity >= CorridorSeverity.Warning)
            ? "Review Recommended"
            : "Healthy";
    }

    private static CorridorStatistics? BuildStatistics(IReadOnlyList<CorridorSummary> summaries)
    {
        if (summaries.Count == 0)
        {
            return new CorridorStatistics();
        }

        int withBaselines = summaries.Count(c => c.BaselineCount > 0);
        int withSurfaces = summaries.Count(c => c.CorridorSurfaceCount > 0);

        return new CorridorStatistics
        {
            CorridorCount = summaries.Count,
            TotalBaselineCount = summaries.Sum(c => c.BaselineCount),
            TotalCorridorSurfaceCount = summaries.Sum(c => c.CorridorSurfaceCount),
            CorridorsWithBaselines = withBaselines,
            CorridorsWithoutBaselines = summaries.Count - withBaselines,
            CorridorsWithSurfaces = withSurfaces,
            CorridorsWithoutSurfaces = summaries.Count - withSurfaces,
            AverageBaselinesPerCorridor = Math.Round(
                (double)summaries.Sum(c => c.BaselineCount) / summaries.Count, 2),
        };
    }
}
