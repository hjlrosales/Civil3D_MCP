using System.Diagnostics;
using Civil3D.Tools.Validation.Dtos;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// Default <see cref="IValidationEngine"/>. Runs every registered rule in order, times each one,
/// isolates per-rule failures (a failing rule is logged and skipped, never aborting the run),
/// honours cancellation between rules and aggregates the findings into categories, a severity
/// summary and top-level recommendations. Rules are constructor-injected and stateless.
/// </summary>
public sealed class ValidationEngine : IValidationEngine
{
    private readonly IReadOnlyList<IValidationRule> _rules;
    private readonly ILogger<ValidationEngine> _logger;

    /// <summary>Creates the engine with the rules to execute.</summary>
    /// <param name="rules">The registered rules; discovered through the container so new rules
    /// compose without engine changes.</param>
    /// <param name="logger">Logger for rule-level diagnostics.</param>
    public ValidationEngine(IEnumerable<IValidationRule> rules, ILogger<ValidationEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(logger);
        _rules = rules.ToArray();
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<IValidationRule> Rules => _rules;

    /// <inheritdoc />
    public RuleExecutionResult ExecuteRules(ValidationData data, IValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<ValidationIssue>();
        int executed = 0;
        int failures = 0;

        foreach (IValidationRule rule in _rules)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var timer = Stopwatch.StartNew();
            try
            {
                int before = issues.Count;
                issues.AddRange(rule.Evaluate(data, context));
                timer.Stop();
                executed++;
                _logger.LogInformation(
                    "Validation rule {Rule} completed in {Elapsed} ms with {Count} finding(s) "
                    + "(correlation {CorrelationId}, session {SessionId}).",
                    rule.Name, timer.ElapsedMilliseconds, issues.Count - before,
                    context.CorrelationId, context.SessionId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                timer.Stop();
                failures++;
                _logger.LogError(
                    ex, "Validation rule {Rule} failed after {Elapsed} ms (correlation {CorrelationId}, "
                    + "session {SessionId}); its findings were skipped.",
                    rule.Name, timer.ElapsedMilliseconds, context.CorrelationId, context.SessionId);
            }
        }

        return new RuleExecutionResult(issues, _rules.Count, executed, failures);
    }

    /// <inheritdoc />
    public IValidationResult Aggregate(RuleExecutionResult execution, int objectCount)
    {
        ArgumentNullException.ThrowIfNull(execution);

        // Sort severity-descending, then code-ascending for a stable report order.
        ValidationIssue[] ordered = execution.Issues
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ToArray();

        ValidationSummary summary = BuildSummary(ordered, objectCount, execution);
        return new ValidationEngineResult(
            ordered,
            BuildCategories(ordered),
            summary,
            BuildRecommendations(ordered));
    }

    private ValidationSummary BuildSummary(
        IReadOnlyList<ValidationIssue> issues, int objectCount, RuleExecutionResult execution)
        => new()
        {
            TotalIssues = issues.Count,
            InformationCount = issues.Count(i => i.Severity == ValidationSeverity.Information),
            WarningCount = issues.Count(i => i.Severity == ValidationSeverity.Warning),
            ErrorCount = issues.Count(i => i.Severity == ValidationSeverity.Error),
            CriticalCount = issues.Count(i => i.Severity == ValidationSeverity.Critical),
            RulesRegistered = execution.RulesRegistered,
            RulesExecuted = execution.RulesExecuted,
            RuleFailures = execution.RuleFailures,
            ObjectCount = objectCount,
        };

    private static IReadOnlyList<ValidationCategory> BuildCategories(IReadOnlyList<ValidationIssue> issues)
        => issues
            .GroupBy(i => i.Category)
            .Select(g => new ValidationCategory
            {
                Name = g.Key,
                TotalIssues = g.Count(),
                InformationCount = g.Count(i => i.Severity == ValidationSeverity.Information),
                WarningCount = g.Count(i => i.Severity == ValidationSeverity.Warning),
                ErrorCount = g.Count(i => i.Severity == ValidationSeverity.Error),
                CriticalCount = g.Count(i => i.Severity == ValidationSeverity.Critical),
            })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ValidationRecommendation> BuildRecommendations(
        IReadOnlyList<ValidationIssue> issues)
    {
        var recommendations = new List<ValidationRecommendation>();
        int critical = issues.Count(i => i.Severity == ValidationSeverity.Critical);
        int errors = issues.Count(i => i.Severity == ValidationSeverity.Error);

        if (issues.Count == 0)
        {
            recommendations.Add(new ValidationRecommendation
            {
                Title = "No findings",
                Description = "The drawing passed every registered validation rule.",
                Severity = ValidationSeverity.Information,
                SuggestedAction = "No action required.",
            });
            return recommendations;
        }

        if (critical > 0)
        {
            recommendations.Add(new ValidationRecommendation
            {
                Title = "Resolve critical findings",
                Description = $"Resolve {critical} critical finding{(critical == 1 ? string.Empty : "s")}.",
                Severity = ValidationSeverity.Critical,
                SuggestedAction = "Address the critical findings before further work.",
            });
        }

        if (errors > 0)
        {
            recommendations.Add(new ValidationRecommendation
            {
                Title = "Fix error findings",
                Description = $"Fix {errors} error finding{(errors == 1 ? string.Empty : "s")}.",
                Severity = ValidationSeverity.Error,
                SuggestedAction = "Repair the broken or unresolved references.",
            });
        }

        recommendations.Add(new ValidationRecommendation
        {
            Title = "Review all findings",
            Description = $"Review all {issues.Count} finding{(issues.Count == 1 ? string.Empty : "s")}.",
            Severity = ValidationSeverity.Warning,
            SuggestedAction = "Work through the findings by severity, highest first.",
        });

        return recommendations;
    }
}
