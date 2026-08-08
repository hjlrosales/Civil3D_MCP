using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Project.Dtos;

namespace Civil3D.Tools.Project.Analysis;

/// <summary>
/// Pure, Autodesk-free analysis engine for the project summary report. Turns a materialized
/// <see cref="ProjectData"/> snapshot into the overview, object inventory, reference integrity
/// summary, complexity classification, statistics and recommendations. The class holds no state;
/// every rule is a static method, so it is trivially testable.
/// </summary>
public static class ProjectAnalyzer
{
    /// <summary>Runs every analysis rule over the given data.</summary>
    /// <param name="data">The materialized drawing and domain data.</param>
    /// <param name="options">Optional thresholds; defaults apply when omitted.</param>
    public static ProjectAnalysisResult Analyze(ProjectData data, ProjectSummaryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ProjectSummaryOptions opts = options ?? ProjectSummaryOptions.Default;

        ProjectOverview overview = BuildOverview(data);
        ObjectInventory inventory = BuildInventory(data, opts);
        ReferenceSummary references = BuildReferences(data);
        ComplexityAssessment complexity = AssessComplexity(data, opts);
        ProjectStatistics statistics = BuildStatistics(data, inventory, references);
        IReadOnlyList<ProjectRecommendation> recommendations = BuildRecommendations(data, inventory, references, opts);

        return new ProjectAnalysisResult(overview, inventory, references, complexity, statistics, recommendations);
    }

    private static ProjectOverview BuildOverview(ProjectData data)
    {
        ActiveDrawing drawing = data.Drawing;
        return new ProjectOverview
        {
            DrawingName = drawing.DrawingName,
            DrawingPath = drawing.DrawingPath,
            DrawingVersion = drawing.DrawingVersion,
            Civil3DVersion = drawing.Civil3DVersion,
            IsModified = drawing.IsModified,
            IsReadOnly = drawing.IsReadOnly,
            CurrentLayout = drawing.CurrentLayout,
            DatabaseFingerprint = drawing.DatabaseFingerprint,
            OpenDocumentsCount = drawing.OpenDocumentsCount,
        };
    }

    private static ObjectInventory BuildInventory(ProjectData data, ProjectSummaryOptions opts)
    {
        DrawingStatistics? stats = data.Statistics;
        int pipeCount = data.PipeNetworks.Sum(n => n.PipeCount);
        int structureCount = data.PipeNetworks.Sum(n => n.StructureCount);

        int maxNames = Math.Max(1, opts.MaxNameListLength);
        bool namesTruncated = data.Alignments.Count > maxNames
            || data.Surfaces.Count > maxNames
            || data.Corridors.Count > maxNames
            || data.PipeNetworks.Count > maxNames;

        return new ObjectInventory
        {
            AlignmentCount = data.Alignments.Count,
            ProfileCount = data.Profiles.Count,
            SurfaceCount = data.Surfaces.Count,
            CorridorCount = data.Corridors.Count,
            PipeNetworkCount = data.PipeNetworks.Count,
            PipeCount = pipeCount,
            StructureCount = structureCount,
            CogoPointCount = data.CogoPoints.Count,
            StyleCount = data.Styles.Count,
            LayerCount = stats?.LayerCount ?? 0,
            BlockCount = stats?.BlockCount ?? 0,
            XRefCount = stats?.XRefCount ?? 0,
            EntityCount = stats?.EntityCount ?? 0,
            ModelSpaceEntityCount = stats?.ModelSpaceEntityCount ?? 0,
            PaperSpaceEntityCount = stats?.PaperSpaceEntityCount ?? 0,
            ViewportCount = stats?.ViewportCount ?? 0,
            TextStyleCount = stats?.TextStyleCount ?? 0,
            DimensionStyleCount = stats?.DimensionStyleCount ?? 0,
            LinetypeCount = stats?.LinetypeCount ?? 0,
            AlignmentNames = Cap(data.Alignments.Select(a => a.Name), maxNames),
            SurfaceNames = Cap(data.Surfaces.Select(s => s.Name), maxNames),
            CorridorNames = Cap(data.Corridors.Select(c => c.Name), maxNames),
            PipeNetworkNames = Cap(data.PipeNetworks.Select(n => n.Name), maxNames),
            NamesTruncated = namesTruncated,
        };
    }

    private static ReferenceSummary BuildReferences(ProjectData data)
    {
        int xrefs = data.Statistics?.XRefCount ?? 0;

        HashSet<long> alignmentIds = data.Alignments.Select(a => a.Id).ToHashSet();
        HashSet<long> styleIds = data.Styles.Select(s => s.Id).ToHashSet();

        int orphanedProfiles = data.Profiles.Count(p => !alignmentIds.Contains(p.AlignmentId));
        int orphanedCorridors = data.Corridors.Count(
            c => c.AlignmentId is { } id && !alignmentIds.Contains(id));
        int missingStyles = 0;

        foreach (AlignmentInfo alignment in data.Alignments)
        {
            if (alignment.StyleId is { } alignmentStyleId && !styleIds.Contains(alignmentStyleId))
            {
                missingStyles++;
            }
        }

        foreach (CorridorInfo corridor in data.Corridors)
        {
            if (corridor.StyleId is { } corridorStyleId && !styleIds.Contains(corridorStyleId))
            {
                missingStyles++;
            }

            if (corridor.CodeSetStyleId is { } codeSetStyleId && !styleIds.Contains(codeSetStyleId))
            {
                missingStyles++;
            }
        }

        int objectReferences = data.Profiles.Count
            + data.Corridors.Count
            + data.Alignments.Count(a => a.StyleId is not null)
            + data.Corridors.Count(c => c.StyleId is not null)
            + data.Corridors.Count(c => c.CodeSetStyleId is not null);
        int totalChecked = xrefs + objectReferences;
        int missing = orphanedProfiles + orphanedCorridors + missingStyles;
        int healthy = totalChecked - missing;
        bool isHealthy = missing == 0;

        return new ReferenceSummary
        {
            TotalXRefs = xrefs,
            TotalReferencesChecked = totalChecked,
            HealthyReferenceCount = healthy,
            MissingReferenceCount = missing,
            OrphanedObjectCount = orphanedProfiles + orphanedCorridors,
            MissingStyleCount = missingStyles,
            IsHealthy = isHealthy,
            Status = isHealthy ? "Healthy" : "Issues Found",
        };
    }

    private static ComplexityAssessment AssessComplexity(ProjectData data, ProjectSummaryOptions opts)
    {
        int entities = data.Statistics?.EntityCount ?? 0;
        int xrefs = data.Statistics?.XRefCount ?? 0;

        double score = entities / 5_000.0
            + xrefs * 3.0
            + data.ObjectCount / 20.0
            + data.Corridors.Count * 4.0
            + data.PipeNetworks.Count * 3.0
            + data.Surfaces.Count * 2.0;

        ProjectComplexity classification;
        if (score < opts.SmallScoreThreshold)
        {
            classification = ProjectComplexity.Small;
        }
        else if (score < opts.MediumScoreThreshold)
        {
            classification = ProjectComplexity.Medium;
        }
        else if (score < opts.LargeScoreThreshold)
        {
            classification = ProjectComplexity.Large;
        }
        else
        {
            classification = ProjectComplexity.Enterprise;
        }

        string reason = $"Entity volume {entities}; {xrefs} external reference(s); "
            + $"{data.ObjectCount} domain objects; {data.Corridors.Count} corridor(s); "
            + $"{data.PipeNetworks.Count} pipe network(s); {data.Surfaces.Count} surface(s).";

        return new ComplexityAssessment
        {
            Classification = classification,
            Score = Math.Round(score, 2),
            Reason = reason,
        };
    }

    private static ProjectStatistics BuildStatistics(
        ProjectData data, ObjectInventory inventory, ReferenceSummary references)
        => new()
        {
            TotalDomainObjects = data.ObjectCount,
            TotalEntities = inventory.EntityCount,
            TotalXRefs = inventory.XRefCount,
            TotalReferencesChecked = references.TotalReferencesChecked,
            HealthyReferenceCount = references.HealthyReferenceCount,
            MissingReferenceCount = references.MissingReferenceCount,
        };

    private static IReadOnlyList<ProjectRecommendation> BuildRecommendations(
        ProjectData data, ObjectInventory inventory, ReferenceSummary references, ProjectSummaryOptions opts)
    {
        var recommendations = new List<ProjectRecommendation>();

        // Broken references are the highest-priority issue.
        if (references.MissingReferenceCount > 0)
        {
            recommendations.Add(new ProjectRecommendation
            {
                Title = "Audit broken references",
                Description = $"{references.MissingReferenceCount} of {references.TotalReferencesChecked} checked references "
                    + $"did not resolve ({references.OrphanedObjectCount} orphaned object(s), "
                    + $"{references.MissingStyleCount} missing style reference(s)).",
                Priority = RecommendationPriority.High,
                SuggestedAction = "Re-associate or remove objects that reference missing alignments or styles.",
            });
        }

        // Unused object styles.
        int unusedStyles = CountUnusedStyles(data);
        if (unusedStyles > 0)
        {
            recommendations.Add(new ProjectRecommendation
            {
                Title = "Review unused styles",
                Description = $"{unusedStyles} alignment/corridor style(s) are not referenced by any inspected object.",
                Priority = unusedStyles > 3 ? RecommendationPriority.Medium : RecommendationPriority.Low,
                SuggestedAction = "Remove styles that are no longer needed.",
            });
        }

        // Large drawing.
        if (inventory.EntityCount >= opts.LargeDrawingEntityThreshold)
        {
            recommendations.Add(new ProjectRecommendation
            {
                Title = "Large drawing optimization",
                Description = $"The drawing contains {inventory.EntityCount} entities.",
                Priority = RecommendationPriority.Medium,
                SuggestedAction = "Review unused content and purge unreferenced objects.",
            });
        }

        // Missing metadata (objects without descriptions).
        int missingDescriptions = data.Alignments.Count(a => string.IsNullOrWhiteSpace(a.Description))
            + data.Surfaces.Count(s => string.IsNullOrWhiteSpace(s.Description))
            + data.Profiles.Count(p => string.IsNullOrWhiteSpace(p.Description))
            + data.Corridors.Count(c => string.IsNullOrWhiteSpace(c.Description))
            + data.PipeNetworks.Count(n => string.IsNullOrWhiteSpace(n.Description))
            + data.CogoPoints.Count(p => string.IsNullOrWhiteSpace(p.FullDescription));
        if (missingDescriptions > 0)
        {
            recommendations.Add(new ProjectRecommendation
            {
                Title = "Missing metadata",
                Description = $"{missingDescriptions} object(s) have no description.",
                Priority = RecommendationPriority.Low,
                SuggestedAction = "Add short descriptions to the flagged objects.",
            });
        }

        // Reference synchronization is only actionable when xrefs exist.
        if (inventory.XRefCount > 0)
        {
            recommendations.Add(new ProjectRecommendation
            {
                Title = "Reference synchronization",
                Description = $"The drawing references {inventory.XRefCount} external drawing(s).",
                Priority = RecommendationPriority.Low,
                SuggestedAction = "Verify referenced drawings are current and paths resolve.",
            });
        }

        // Highest priority first, then title for stable ordering.
        return recommendations
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountUnusedStyles(ProjectData data)
    {
        var referencedAlignmentStyles = data.Alignments.Select(a => a.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCorridorStyles = data.Corridors.Select(c => c.StyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();
        var referencedCodeSetStyles = data.Corridors.Select(c => c.CodeSetStyleId).Where(id => id is not null)
            .Select(id => id!.Value).ToHashSet();

        return data.Styles.Count(style => style.Kind switch
        {
            StyleKind.Alignment => !referencedAlignmentStyles.Contains(style.Id),
            StyleKind.Corridor => !referencedCorridorStyles.Contains(style.Id)
                                  && !referencedCodeSetStyles.Contains(style.Id),
            _ => false,
        });
    }

    private static IReadOnlyList<string> Cap(IEnumerable<string> names, int max)
        => names.Take(max).ToArray();
}
