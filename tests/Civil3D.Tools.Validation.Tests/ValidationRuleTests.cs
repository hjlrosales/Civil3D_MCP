using Civil3D.Domain.Alignments.Dtos;
using Civil3D.Domain.Cogo.Dtos;
using Civil3D.Domain.Corridors.Dtos;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Profiles.Dtos;
using Civil3D.Domain.Styles.Dtos;
using Civil3D.Domain.Surfaces.Dtos;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Rules;
using Xunit;
using static Civil3D.Tools.Validation.Tests.TestDoubles;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// Each validation rule in isolation against crafted data: what it flags, what it ignores, and
/// the severity/category of its findings.
/// </summary>
public class ValidationRuleTests
{
    private static readonly IValidationContext Ctx = new ValidationContext("c", "s", CancellationToken.None);

    [Fact]
    public void DuplicateNames_FlagsDuplicateWithinCollection()
    {
        var data = new ValidationData
        {
            Alignments =
            [
                new AlignmentInfo { Id = 1, Name = "Main" },
                new AlignmentInfo { Id = 2, Name = "Main" },
            ],
        };

        var issues = new DuplicateNameRule().Evaluate(data, Ctx);

        var issue = Assert.Single(issues);
        Assert.Equal("DUPLICATE_ALIGNMENT_NAME", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Names", issue.Category);
        Assert.Equal("Main", issue.RelatedObject);
    }

    [Fact]
    public void DuplicateNames_IgnoresUniqueNamesAcrossCollections()
    {
        var data = new ValidationData
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "Same" }],
            Surfaces = [new SurfaceInfo { Id = 1, Name = "Same" }],
        };

        Assert.Empty(new DuplicateNameRule().Evaluate(data, Ctx));
    }

    [Fact]
    public void MissingDescriptions_FlagsUndocumentedObjects()
    {
        var data = new ValidationData
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", Description = null }],
            Surfaces = [new SurfaceInfo { Id = 1, Name = "S", Description = "has one" }],
            CogoPoints = [new CogoPointInfo { Id = 1, PointNumber = 5, FullDescription = null }],
        };

        var issues = new MissingDescriptionRule().Evaluate(data, Ctx);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Code == "MISSING_ALIGNMENT_DESCRIPTION");
        Assert.Contains(issues, i => i.Code == "MISSING_COGO_POINT_DESCRIPTION");
        Assert.All(issues, i => Assert.Equal(ValidationSeverity.Information, i.Severity));
    }

    [Fact]
    public void EmptyCollections_FlagsEmptyWhenDrawingHasContent()
    {
        var data = new ValidationData
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A" }],
            Surfaces = [],
            Corridors = [],
        };

        var issues = new EmptyCollectionRule().Evaluate(data, Ctx);

        Assert.Contains(issues, i => i.Code == "EMPTY_SURFACES");
        Assert.Contains(issues, i => i.Code == "EMPTY_CORRIDORS");
        Assert.DoesNotContain(issues, i => i.Code == "EMPTY_ALIGNMENTS");
    }

    [Fact]
    public void EmptyCollections_SilentOnBlankDrawing()
    {
        Assert.Empty(new EmptyCollectionRule().Evaluate(new ValidationData(), Ctx));
    }

    [Fact]
    public void UnresolvedReferences_FlagsMissingAlignmentAndStyle()
    {
        var data = new ValidationData
        {
            Alignments =
            [
                new AlignmentInfo { Id = 1, Name = "A", StyleId = 99 },
            ],
            Styles = [new StyleInfo { Id = 1, Name = "Road", Kind = StyleKind.Alignment }],
            Profiles = [new ProfileInfo { Id = 1, Name = "P", AlignmentId = 42 }],
            Corridors = [new CorridorInfo { Id = 1, Name = "C", AlignmentId = 7 }],
        };

        var issues = new UnresolvedReferenceRule().Evaluate(data, Ctx);

        Assert.Contains(issues, i => i.Code == "UNRESOLVED_STYLE_REFERENCE" && i.RelatedObject == "A");
        Assert.Contains(issues, i => i.Code == "UNRESOLVED_ALIGNMENT_REFERENCE" && i.RelatedObject == "P");
        Assert.Contains(issues, i => i.Code == "UNRESOLVED_ALIGNMENT_REFERENCE" && i.RelatedObject == "C");
        Assert.All(issues, i => Assert.Equal(ValidationSeverity.Error, i.Severity));
    }

    [Fact]
    public void UnresolvedReferences_ResolvedReferencesAreSilent()
    {
        var data = new ValidationData
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 1 }],
            Styles = [new StyleInfo { Id = 1, Name = "Road", Kind = StyleKind.Alignment }],
            Profiles = [new ProfileInfo { Id = 1, Name = "P", AlignmentId = 1 }],
            Corridors =
            [
                new CorridorInfo { Id = 1, Name = "C", AlignmentId = 1, StyleId = 1, CodeSetStyleId = 1 },
            ],
        };

        Assert.Empty(new UnresolvedReferenceRule().Evaluate(data, Ctx));
    }

    [Fact]
    public void UnusedStyles_FlagsOnlyCheckableKinds()
    {
        var data = new ValidationData
        {
            Alignments = [new AlignmentInfo { Id = 1, Name = "A", StyleId = 1 }],
            Styles =
            [
                new StyleInfo { Id = 1, Name = "Used", Kind = StyleKind.Alignment },
                new StyleInfo { Id = 2, Name = "Unused", Kind = StyleKind.Alignment },
                new StyleInfo { Id = 3, Name = "Label", Kind = StyleKind.Other },
            ],
        };

        var issues = new UnusedStyleRule().Evaluate(data, Ctx);

        var issue = Assert.Single(issues);
        Assert.Equal("Unused", issue.RelatedObject);
        Assert.Equal(ValidationSeverity.Information, issue.Severity);
    }

    [Fact]
    public void DuplicateCogoNumbers_FlagsSharedPointNumbers()
    {
        var data = new ValidationData
        {
            CogoPoints =
            [
                new CogoPointInfo { Id = 1, PointNumber = 7 },
                new CogoPointInfo { Id = 2, PointNumber = 7 },
                new CogoPointInfo { Id = 3, PointNumber = 8 },
            ],
        };

        var issues = new DuplicateCogoPointNumberRule().Evaluate(data, Ctx);

        var issue = Assert.Single(issues);
        Assert.Equal("DUPLICATE_COGO_POINT_NUMBER", issue.Code);
        Assert.Equal("7", issue.RelatedObject);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void ProfilesWithoutAlignment_FlagsAlignmentIdZero()
    {
        var data = new ValidationData
        {
            Profiles =
            [
                new ProfileInfo { Id = 1, Name = "P", AlignmentId = 0 },
                new ProfileInfo { Id = 2, Name = "Q", AlignmentId = 1 },
            ],
        };

        var issues = new ProfileWithoutAlignmentRule().Evaluate(data, Ctx);

        var issue = Assert.Single(issues);
        Assert.Equal("PROFILE_WITHOUT_ALIGNMENT", issue.Code);
        Assert.Equal("P", issue.RelatedObject);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void PipeNetworksWithoutStructures_FlagsPipesOnlyNetworks()
    {
        var data = new ValidationData
        {
            PipeNetworks =
            [
                new PipeNetworkInfo { Id = 1, Name = "Storm", Pipes = [new PipeInfo { Id = 1, Name = "P-1" }] },
                new PipeNetworkInfo
                {
                    Id = 2,
                    Name = "Sanitary",
                    Pipes = [new PipeInfo { Id = 2, Name = "P-2" }],
                    Structures = [new StructureInfo { Id = 1, Name = "S-1" }],
                },
            ],
        };

        var issues = new PipeNetworkWithoutStructureRule().Evaluate(data, Ctx);

        var issue = Assert.Single(issues);
        Assert.Equal("PIPE_NETWORK_WITHOUT_STRUCTURES", issue.Code);
        Assert.Equal("Storm", issue.RelatedObject);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Rules_AreStatelessAndCancellable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = new ValidationContext("c", "s", cts.Token);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new DuplicateNameRule().Evaluate(new ValidationData(), cancelled));
    }
}
