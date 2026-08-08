using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Health.Dtos;

namespace Civil3D.Tools.Health.Analysis;

/// <summary>
/// Pure, Autodesk-free analysis engine for the drawing health report. Turns a materialized
/// <see cref="HealthData"/> snapshot into findings (empty collections, duplicate names, missing
/// descriptions, orphaned references, missing styles, unused styles, large collections, locked
/// points, drawing state), category roll-ups, top-level recommendations and severity statistics.
/// The class holds no state — every rule is a static method — so it is trivially testable.
/// </summary>
public static class HealthAnalyzer
{
    private const string DrawingCategory = "Drawing";
    private const string AlignmentsCategory = "Alignments";
    private const string SurfacesCategory = "Surfaces";
    private const string ProfilesCategory = "Profiles";
    private const string CorridorsCategory = "Corridors";
    private const string PipeNetworksCategory = "Pipe Networks";
    private const string CogoPointsCategory = "COGO Points";
    private const string StylesCategory = "Styles";

    /// <summary>Runs every rule over the given data.</summary>
    /// <param name="data">The materialized drawing and domain data.</param>
    /// <param name="options">Optional thresholds; defaults apply when omitted.</param>
    public static HealthAnalysisResult Analyze(HealthData data, HealthAnalyzerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        HealthAnalyzerOptions opts = options ?? HealthAnalyzerOptions.Default;

        var issues = new List<HealthIssue>();
        AnalyzeDrawing(data, opts, issues);
        AnalyzeAlignments(data, issues);
        AnalyzeSurfaces(data, opts, issues);
        AnalyzeProfiles(data, issues);
        AnalyzeCorridors(data, issues);
        AnalyzePipeNetworks(data, issues);
        AnalyzeCogoPoints(data, opts, issues);
        AnalyzeStyles(data, issues);

        issues.Sort(static (a, b) =>
        {
            int bySeverity = b.Severity.CompareTo(a.Severity);
            return bySeverity != 0 ? bySeverity : string.CompareOrdinal(a.Code, b.Code);
        });

        return new HealthAnalysisResult(
            issues,
            BuildCategories(issues),
            BuildRecommendations(issues),
            BuildStatistics(issues, data.ObjectCount));
    }

    private static void AnalyzeDrawing(HealthData data, HealthAnalyzerOptions opts, List<HealthIssue> issues)
    {
        if (data.Statistics is { } stats)
        {
            if (stats.EntityCount >= opts.LargeDrawingEntityThreshold)
            {
                issues.Add(Issue(
                    "LARGE_DRAWING", HealthSeverity.Warning, DrawingCategory,
                    $"The drawing contains {stats.EntityCount} entities.",
                    "Very large drawings can degrade performance and increase save times.",
                    "Review unused content and purge unreferenced objects."));
            }

            if (stats.ModelSpaceEntityCount >= opts.LargeModelSpaceEntityThreshold)
            {
                issues.Add(Issue(
                    "LARGE_MODEL_SPACE", HealthSeverity.Warning, DrawingCategory,
                    $"Model space contains {stats.ModelSpaceEntityCount} entities.",
                    "A dense model space slows zoom, pan and regen operations.",
                    "Move non-production content to paper space layouts or separate drawings."));
            }
        }

        if (data.Drawing.IsReadOnly)
        {
            issues.Add(Issue(
                "READ_ONLY_DRAWING", HealthSeverity.Warning, DrawingCategory,
                "The drawing file is read-only.",
                "Changes cannot be saved to a read-only file, which may mask future edit failures.",
                "Clear the read-only attribute before editing the drawing."));
        }

        if (data.Drawing.IsModified)
        {
            issues.Add(Issue(
                "UNSAVED_CHANGES", HealthSeverity.Information, DrawingCategory,
                "The drawing contains unsaved changes.",
                "Unsaved changes are lost if the session terminates unexpectedly.",
                "Save the drawing to persist the current state."));
        }
    }

    private static void AnalyzeAlignments(HealthData data, List<HealthIssue> issues)
    {
        if (data.Alignments.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_ALIGNMENTS", HealthSeverity.Information, AlignmentsCategory,
                "The drawing contains no alignments.",
                "No alignment geometry exists in this drawing.",
                "Confirm alignments are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddDuplicateIssues(data.Alignments, a => a.Name, "DUPLICATE_ALIGNMENT_NAME",
            AlignmentsCategory, "alignment", issues);
        AddMissingDescriptionIssues(data.Alignments, a => a.Name, a => a.Description,
            "MISSING_ALIGNMENT_DESCRIPTION", AlignmentsCategory, "alignment", issues);
        AddMissingStyleIssues(data.Alignments, a => a.Name, a => a.StyleId,
            AlignmentsCategory, "alignment", data, issues);
    }

    private static void AnalyzeSurfaces(HealthData data, HealthAnalyzerOptions opts, List<HealthIssue> issues)
    {
        if (data.Surfaces.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_SURFACES", HealthSeverity.Information, SurfacesCategory,
                "The drawing contains no surfaces.",
                "No surface models exist in this drawing.",
                "Confirm surfaces are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddDuplicateIssues(data.Surfaces, s => s.Name, "DUPLICATE_SURFACE_NAME",
            SurfacesCategory, "surface", issues);
        AddMissingDescriptionIssues(data.Surfaces, s => s.Name, s => s.Description,
            "MISSING_SURFACE_DESCRIPTION", SurfacesCategory, "surface", issues);

        foreach (SurfaceInfo surface in data.Surfaces)
        {
            if (surface.PointCount >= opts.LargeSurfacePointThreshold)
            {
                issues.Add(Issue(
                    "LARGE_SURFACE", HealthSeverity.Warning, SurfacesCategory,
                    $"Surface '{surface.Name}' contains {surface.PointCount} points.",
                    "Large surfaces increase rebuild and display times.",
                    "Review the surface definition for redundant points.",
                    surface.Name));
            }
        }
    }

    private static void AnalyzeProfiles(HealthData data, List<HealthIssue> issues)
    {
        if (data.Profiles.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_PROFILES", HealthSeverity.Information, ProfilesCategory,
                "The drawing contains no profiles.",
                "No profile geometry exists in this drawing.",
                "Confirm profiles are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddDuplicateIssues(data.Profiles, p => p.Name, "DUPLICATE_PROFILE_NAME",
            ProfilesCategory, "profile", issues);
        AddMissingDescriptionIssues(data.Profiles, p => p.Name, p => p.Description,
            "MISSING_PROFILE_DESCRIPTION", ProfilesCategory, "profile", issues);

        HashSet<long> alignmentIds = data.Alignments.Select(a => a.Id).ToHashSet();
        foreach (ProfileInfo profile in data.Profiles)
        {
            if (!alignmentIds.Contains(profile.AlignmentId))
            {
                issues.Add(Issue(
                    "ORPHANED_PROFILE", HealthSeverity.Error, ProfilesCategory,
                    $"Profile '{profile.Name}' references alignment id {profile.AlignmentId}, which does not exist.",
                    "The profile has no valid owning alignment and may not display correctly.",
                    "Re-associate the profile with an existing alignment or remove it.",
                    profile.Name));
            }
        }
    }

    private static void AnalyzeCorridors(HealthData data, List<HealthIssue> issues)
    {
        if (data.Corridors.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_CORRIDORS", HealthSeverity.Information, CorridorsCategory,
                "The drawing contains no corridors.",
                "No corridor models exist in this drawing.",
                "Confirm corridors are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddDuplicateIssues(data.Corridors, c => c.Name, "DUPLICATE_CORRIDOR_NAME",
            CorridorsCategory, "corridor", issues);
        AddMissingDescriptionIssues(data.Corridors, c => c.Name, c => c.Description,
            "MISSING_CORRIDOR_DESCRIPTION", CorridorsCategory, "corridor", issues);

        HashSet<long> alignmentIds = data.Alignments.Select(a => a.Id).ToHashSet();
        foreach (CorridorInfo corridor in data.Corridors)
        {
            if (corridor.AlignmentId is { } alignmentId && !alignmentIds.Contains(alignmentId))
            {
                issues.Add(Issue(
                    "ORPHANED_CORRIDOR", HealthSeverity.Error, CorridorsCategory,
                    $"Corridor '{corridor.Name}' references alignment id {alignmentId}, which does not exist.",
                    "The corridor has no valid baseline alignment and may fail to rebuild.",
                    "Re-associate the corridor with an existing alignment or remove it.",
                    corridor.Name));
            }

            if (corridor.StyleId is { } styleId && !data.Styles.Any(s => s.Id == styleId))
            {
                issues.Add(Issue(
                    "MISSING_STYLE", HealthSeverity.Error, CorridorsCategory,
                    $"Corridor '{corridor.Name}' references style id {styleId}, which does not exist.",
                    "The corridor has no valid display style and may fall back to defaults.",
                    "Re-assign a valid corridor style.",
                    corridor.Name));
            }

            if (corridor.CodeSetStyleId is { } codeSetId && !data.Styles.Any(s => s.Id == codeSetId))
            {
                issues.Add(Issue(
                    "MISSING_CODE_SET_STYLE", HealthSeverity.Error, CorridorsCategory,
                    $"Corridor '{corridor.Name}' references code set style id {codeSetId}, which does not exist.",
                    "Code set styles control labeling; a missing style disables expected labels.",
                    "Re-assign a valid code set style.",
                    corridor.Name));
            }
        }
    }

    private static void AnalyzePipeNetworks(HealthData data, List<HealthIssue> issues)
    {
        if (data.PipeNetworks.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_PIPE_NETWORKS", HealthSeverity.Information, PipeNetworksCategory,
                "The drawing contains no pipe networks.",
                "No pipe or structure networks exist in this drawing.",
                "Confirm pipe networks are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddDuplicateIssues(data.PipeNetworks, n => n.Name, "DUPLICATE_PIPE_NETWORK_NAME",
            PipeNetworksCategory, "pipe network", issues);
        AddMissingDescriptionIssues(data.PipeNetworks, n => n.Name, n => n.Description,
            "MISSING_PIPE_NETWORK_DESCRIPTION", PipeNetworksCategory, "pipe network", issues);
    }

    private static void AnalyzeCogoPoints(HealthData data, HealthAnalyzerOptions opts, List<HealthIssue> issues)
    {
        if (data.CogoPoints.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_COGO_POINTS", HealthSeverity.Information, CogoPointsCategory,
                "The drawing contains no COGO points.",
                "No survey or design points exist in this drawing.",
                "Confirm COGO points are expected; otherwise add them or ignore this finding."));
            return;
        }

        AddMissingDescriptionIssues(data.CogoPoints, p => p.PointNumber.ToString(), p => p.FullDescription,
            "MISSING_COGO_POINT_DESCRIPTION", CogoPointsCategory, "COGO point", issues);

        int lockedCount = data.CogoPoints.Count(p => p.IsLocked);
        if (lockedCount > 0)
        {
            issues.Add(Issue(
                "LOCKED_COGO_POINTS", HealthSeverity.Warning, CogoPointsCategory,
                $"{lockedCount} of {data.CogoPoints.Count} COGO points are locked.",
                "Locked points reject edits, which can surprise later editing operations.",
                "Unlock points that should be editable and keep genuinely fixed points locked."));
        }

        if (data.CogoPoints.Count >= opts.LargeCogoPointThreshold)
        {
            issues.Add(Issue(
                "LARGE_COGO_POINT_COLLECTION", HealthSeverity.Warning, CogoPointsCategory,
                $"The drawing contains {data.CogoPoints.Count} COGO points.",
                "Very large point collections can slow point queries and labeling.",
                "Review whether all points are required in this drawing."));
        }
    }

    private static void AnalyzeStyles(HealthData data, List<HealthIssue> issues)
    {
        if (data.Styles.Count == 0)
        {
            issues.Add(Issue(
                "EMPTY_STYLES", HealthSeverity.Information, StylesCategory,
                "The drawing contains no Civil 3D styles.",
                "Styles control object display and labeling; an empty set is unusual.",
                "Confirm the drawing was created from an appropriate template."));
            return;
        }

        // Only object kinds whose referencing objects are exposed by the domain DTOs can be
        // checked for usage; label styles are referenced by labels we do not inspect.
        var referencedAlignmentStyles = data.Alignments.Select(a => a.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCorridorStyles = data.Corridors.Select(c => c.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCodeSetStyles = data.Corridors.Select(c => c.CodeSetStyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();

        foreach (StyleInfo style in data.Styles)
        {
            bool unused = style.Kind switch
            {
                StyleKind.Alignment => !referencedAlignmentStyles.Contains(style.Id),
                StyleKind.Corridor => !referencedCorridorStyles.Contains(style.Id)
                                      && !referencedCodeSetStyles.Contains(style.Id),
                _ => false,
            };

            if (unused)
            {
                issues.Add(Issue(
                    "UNUSED_STYLE", HealthSeverity.Information, StylesCategory,
                    $"Style '{style.Name}' is not referenced by any inspected object.",
                    "Unused styles accumulate in templates and drawings.",
                    "Remove the style if it is no longer needed.",
                    style.Name));
            }
        }
    }

    private static HealthIssue Issue(
        string code, HealthSeverity severity, string category, string description,
        string? reason, string? suggestedAction, string? relatedObject = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Category = category,
            Description = description,
            Reason = reason,
            SuggestedAction = suggestedAction,
            RelatedObject = relatedObject,
        };

    private static void AddDuplicateIssues<T>(
        IReadOnlyList<T> items, Func<T, string> nameSelector, string code, string category,
        string kindLabel, List<HealthIssue> issues)
    {
        foreach (string name in items
                     .GroupBy(nameSelector, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
        {
            issues.Add(Issue(
                code, HealthSeverity.Warning, category,
                $"Multiple {kindLabel}s share the name '{name}'.",
                "Duplicate names make objects hard to distinguish in queries and reports.",
                "Rename the duplicate objects to unique names.",
                name));
        }
    }

    private static void AddMissingDescriptionIssues<T>(
        IReadOnlyList<T> items, Func<T, string> nameSelector, Func<T, string?> descriptionSelector,
        string code, string category, string kindLabel, List<HealthIssue> issues)
    {
        foreach (T item in items)
        {
            if (string.IsNullOrWhiteSpace(descriptionSelector(item)))
            {
                string name = nameSelector(item);
                issues.Add(Issue(
                    code, HealthSeverity.Information, category,
                    $"{kindLabel} '{name}' has no description.",
                    "Descriptions help identify objects in reports and long-lived projects.",
                    "Add a short description to the object.",
                    name));
            }
        }
    }

    private static void AddMissingStyleIssues<T>(
        IReadOnlyList<T> items, Func<T, string> nameSelector, Func<T, long?> styleIdSelector,
        string category, string kindLabel, HealthData data, List<HealthIssue> issues)
    {
        HashSet<long> styleIds = data.Styles.Select(s => s.Id).ToHashSet();
        foreach (T item in items)
        {
            if (styleIdSelector(item) is { } styleId && !styleIds.Contains(styleId))
            {
                string name = nameSelector(item);
                issues.Add(Issue(
                    "MISSING_STYLE", HealthSeverity.Error, category,
                    $"{kindLabel} '{name}' references style id {styleId}, which does not exist.",
                    "The object has no valid display style and may fall back to defaults.",
                    "Re-assign a valid style to the object.",
                    name));
            }
        }
    }

    private static IReadOnlyList<HealthCategory> BuildCategories(IReadOnlyList<HealthIssue> issues)
    {
        return issues
            .GroupBy(i => i.Category)
            .Select(g => new HealthCategory
            {
                Name = g.Key,
                TotalIssues = g.Count(),
                InformationCount = g.Count(i => i.Severity == HealthSeverity.Information),
                WarningCount = g.Count(i => i.Severity == HealthSeverity.Warning),
                ErrorCount = g.Count(i => i.Severity == HealthSeverity.Error),
                CriticalCount = g.Count(i => i.Severity == HealthSeverity.Critical),
            })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<HealthRecommendation> BuildRecommendations(IReadOnlyList<HealthIssue> issues)
    {
        var recommendations = new List<HealthRecommendation>();
        int critical = issues.Count(i => i.Severity == HealthSeverity.Critical);
        int errors = issues.Count(i => i.Severity == HealthSeverity.Error);

        if (issues.Count == 0)
        {
            recommendations.Add(new HealthRecommendation
            {
                Description = "The drawing is healthy.",
                Reason = "No findings were produced.",
                SuggestedAction = "No action required.",
            });
            return recommendations;
        }

        if (critical > 0)
        {
            recommendations.Add(new HealthRecommendation
            {
                Description = $"Resolve {critical} critical finding{(critical == 1 ? string.Empty : "s")}.",
                Reason = "Critical findings are likely to affect production use.",
                SuggestedAction = "Address the critical findings before further work.",
            });
        }

        if (errors > 0)
        {
            recommendations.Add(new HealthRecommendation
            {
                Description = $"Fix {errors} error finding{(errors == 1 ? string.Empty : "s")}.",
                Reason = "Errors indicate broken or invalid object relationships.",
                SuggestedAction = "Repair the orphaned or missing references.",
            });
        }

        recommendations.Add(new HealthRecommendation
        {
            Description = $"Review all {issues.Count} finding{(issues.Count == 1 ? string.Empty : "s")}.",
            Reason = "Warnings and information findings may indicate data-quality issues.",
            SuggestedAction = "Work through the findings by severity, highest first.",
        });

        return recommendations;
    }

    private static HealthStatistics BuildStatistics(IReadOnlyList<HealthIssue> issues, int objectCount)
        => new()
        {
            TotalIssues = issues.Count,
            InformationCount = issues.Count(i => i.Severity == HealthSeverity.Information),
            WarningCount = issues.Count(i => i.Severity == HealthSeverity.Warning),
            ErrorCount = issues.Count(i => i.Severity == HealthSeverity.Error),
            CriticalCount = issues.Count(i => i.Severity == HealthSeverity.Critical),
            ObjectCount = objectCount,
        };
}
