using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// The outcome of running the registered rules: the raw findings plus rule-accounting. Produced
/// by <see cref="IValidationEngine.ExecuteRules"/> and consumed by
/// <see cref="IValidationEngine.Aggregate"/>.
/// </summary>
/// <param name="Issues">The raw findings in execution order (not yet sorted).</param>
/// <param name="RulesRegistered">The number of rules registered with the engine.</param>
/// <param name="RulesExecuted">The number of rules that executed successfully.</param>
/// <param name="RuleFailures">The number of rules that failed and were skipped.</param>
public sealed record RuleExecutionResult(
    IReadOnlyList<ValidationIssue> Issues,
    int RulesRegistered,
    int RulesExecuted,
    int RuleFailures);
