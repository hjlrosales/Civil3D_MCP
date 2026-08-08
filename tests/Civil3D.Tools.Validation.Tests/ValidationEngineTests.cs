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
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Civil3D.Tools.Validation.Tests.ValidationHarness;
using static Civil3D.Tools.Validation.Tests.TestDoubles;

namespace Civil3D.Tools.Validation.Tests;

/// <summary>
/// The validation engine: rule registration/discovery through the container, execution over the
/// canned sample, per-rule failure isolation, cancellation, aggregation (categories, summary,
/// recommendations), ordering and report serialization.
/// </summary>
public class ValidationEngineTests
{
    private static readonly IValidationContext Ctx = new ValidationContext("c", "s", CancellationToken.None);

    private static ValidationData SampleData() => new()
    {
        Drawing = SampleData_Drawing(),
        Statistics = SampleData_Statistics(),
        Alignments = SampleData_Alignments(),
        Surfaces = SampleData_Surfaces(),
        Profiles = SampleData_Profiles(),
        Corridors = SampleData_Corridors(),
        PipeNetworks = SampleData_PipeNetworks(),
        CogoPoints = SampleData_CogoPoints(),
        Styles = SampleData_Styles(),
    };

    private static int SampleDataObjectCount()
        => SampleData_Alignments().Count + SampleData_Surfaces().Count + SampleData_Profiles().Count
           + SampleData_Corridors().Count + SampleData_PipeNetworks().Count
           + SampleData_CogoPoints().Count + SampleData_Styles().Count;

    private static ActiveDrawing SampleData_Drawing() => TestDoubles.SampleData.Drawing();
    private static DrawingStatistics SampleData_Statistics() => TestDoubles.SampleData.Statistics();
    private static IReadOnlyList<AlignmentInfo> SampleData_Alignments() => TestDoubles.SampleData.Alignments();
    private static IReadOnlyList<SurfaceInfo> SampleData_Surfaces() => TestDoubles.SampleData.Surfaces();
    private static IReadOnlyList<ProfileInfo> SampleData_Profiles() => TestDoubles.SampleData.Profiles();
    private static IReadOnlyList<CorridorInfo> SampleData_Corridors() => TestDoubles.SampleData.Corridors();
    private static IReadOnlyList<PipeNetworkInfo> SampleData_PipeNetworks() => TestDoubles.SampleData.PipeNetworks();
    private static IReadOnlyList<CogoPointInfo> SampleData_CogoPoints() => TestDoubles.SampleData.CogoPoints();
    private static IReadOnlyList<StyleInfo> SampleData_Styles() => TestDoubles.SampleData.Styles();

    [Fact]
    public void Engine_DiscoveredRulesThroughContainer()
    {
        Container container = CreateContainer();
        IValidationEngine engine = container.Provider.GetRequiredService<IValidationEngine>();

        Assert.Equal(8, engine.Rules.Count);
        Assert.Contains(engine.Rules, r => r.Name == "duplicate-names");
        Assert.Contains(engine.Rules, r => r.Name == "unresolved-references");
    }

    [Fact]
    public void Execute_OverSampleData_FindsExpectedIssues()
    {
        var engine = new ValidationEngine(DefaultRules(), NullLogger<ValidationEngine>.Instance);

        RuleExecutionResult execution = engine.ExecuteRules(SampleData(), Ctx);
        IValidationResult result = engine.Aggregate(execution, 10);

        Assert.Contains(result.Issues, i => i.Code == "DUPLICATE_ALIGNMENT_NAME");
        Assert.Contains(result.Issues, i => i.Code == "UNRESOLVED_ALIGNMENT_REFERENCE" && i.RelatedObject == "Ghost");
        Assert.Contains(result.Issues, i => i.Code == "UNRESOLVED_STYLE_REFERENCE");
        Assert.Contains(result.Issues, i => i.Code == "UNUSED_STYLE" && i.RelatedObject == "Unused Style");
        Assert.Contains(result.Issues, i => i.Code == "DUPLICATE_COGO_POINT_NUMBER");
        Assert.Contains(result.Issues, i => i.Code == "PROFILE_WITHOUT_ALIGNMENT" && i.RelatedObject == "Orphan");
        Assert.Contains(result.Issues, i => i.Code == "PIPE_NETWORK_WITHOUT_STRUCTURES");
        Assert.Contains(result.Issues, i => i.Code == "EMPTY_SURFACES");
        Assert.Contains(result.Issues, i => i.Code == "MISSING_ALIGNMENT_DESCRIPTION");
    }

    [Fact]
    public void Execute_IssuesOrderedBySeverityThenCode()
    {
        var engine = new ValidationEngine(DefaultRules(), NullLogger<ValidationEngine>.Instance);

        RuleExecutionResult execution = engine.ExecuteRules(SampleData(), Ctx);
        IValidationResult result = engine.Aggregate(execution, 10);

        for (int i = 1; i < result.Issues.Count; i++)
        {
            Assert.True(
                result.Issues[i - 1].Severity >= result.Issues[i].Severity,
                $"{result.Issues[i - 1].Code} before {result.Issues[i].Code}");
        }

        Assert.All(result.Issues, i => Assert.False(string.IsNullOrEmpty(i.Code)));
    }

    [Fact]
    public void Execute_Summary_RollsUpSeverityAndRuleAccounting()
    {
        var engine = new ValidationEngine(DefaultRules(), NullLogger<ValidationEngine>.Instance);

        int objectCount = SampleDataObjectCount();
        RuleExecutionResult execution = engine.ExecuteRules(SampleData(), Ctx);
        IValidationResult result = engine.Aggregate(execution, objectCount);

        Assert.Equal(8, result.Summary.RulesRegistered);
        Assert.Equal(objectCount, result.Summary.ObjectCount);
        Assert.Equal(8, result.Summary.RulesExecuted);
        Assert.Equal(0, result.Summary.RuleFailures);
        Assert.Equal(result.Issues.Count, result.Summary.TotalIssues);
        Assert.True(result.Summary.ErrorCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.True(result.Summary.InformationCount > 0);
        Assert.Equal(0, result.Summary.CriticalCount);
    }

    [Fact]
    public void Execute_FailingRule_IsIsolatedAndCounted()
    {
        var failing = new ThrowingRule();
        var engine = new ValidationEngine(new IValidationRule[] { failing, new DuplicateNameRule() },
            NullLogger<ValidationEngine>.Instance);

        RuleExecutionResult execution = engine.ExecuteRules(new ValidationData(), Ctx);
        IValidationResult result = engine.Aggregate(execution, 0);

        Assert.Equal(1, result.Summary.RuleFailures);
        Assert.Equal(2, result.Summary.RulesRegistered);
        Assert.Equal(1, result.Summary.RulesExecuted);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Execute_PreCancelled_ThrowsBeforeRunningRules()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = new ValidationContext("c", "s", cts.Token);
        var engine = new ValidationEngine(DefaultRules(), NullLogger<ValidationEngine>.Instance);

        Assert.ThrowsAny<OperationCanceledException>(() => engine.ExecuteRules(SampleData(), cancelled));
    }

    [Fact]
    public void Execute_EmptyData_ProducesNoFindings()
    {
        var engine = new ValidationEngine(DefaultRules(), NullLogger<ValidationEngine>.Instance);

        RuleExecutionResult execution = engine.ExecuteRules(new ValidationData(), Ctx);
        IValidationResult result = engine.Aggregate(execution, 0);

        Assert.Empty(result.Issues);
        Assert.Single(result.Recommendations);
        Assert.Equal("No findings", result.Recommendations[0].Title);
    }

    [Fact]
    public void Report_SerializesAndRoundTrips()
    {
        var report = new DesignValidationReport
        {
            DrawingName = "ValidationSample.dwg",
            Summary = new ValidationSummary { TotalIssues = 3, RulesExecuted = 8, ObjectCount = 10 },
            Issues =
            [
                new ValidationIssue
                {
                    Code = "DUPLICATE_ALIGNMENT_NAME",
                    Rule = "duplicate-names",
                    Severity = ValidationSeverity.Warning,
                    Category = "Names",
                    Title = "Duplicate alignment name",
                    Description = "Multiple alignments share the name 'Main Road'.",
                },
            ],
            Categories = [new ValidationCategory { Name = "Names", TotalIssues = 1, WarningCount = 1 }],
            Recommendations = [new ValidationRecommendation { Title = "Review all findings", Severity = ValidationSeverity.Warning }],
            Execution = new ValidationExecutionSummary { WorkflowName = "design.validation.report", TotalSteps = 5, CompletedSteps = 5 },
        };

        string json = JsonSerializer.Serialize(report, SharedJson.Options);
        DesignValidationReport roundTrip = JsonSerializer.Deserialize<DesignValidationReport>(json, SharedJson.Options)!;

        Assert.Equal(report.DrawingName, roundTrip.DrawingName);
        Assert.Equal(report.Summary.TotalIssues, roundTrip.Summary.TotalIssues);
        Assert.Equal(report.Issues[0].Code, roundTrip.Issues[0].Code);
        Assert.Equal(report.Issues[0].Severity, roundTrip.Issues[0].Severity);
        Assert.Equal(report.Recommendations[0].Severity, roundTrip.Recommendations[0].Severity);
        Assert.Equal(report.Execution.TotalSteps, roundTrip.Execution.TotalSteps);
    }

    private sealed class ThrowingRule : IValidationRule
    {
        public string Name => "throwing";
        public string Category => "Test";
        public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
            => throw new InvalidOperationException("boom");
    }
}
