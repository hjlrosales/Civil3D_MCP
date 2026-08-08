using Civil3D.Tools.Validation.Dtos;

namespace Civil3D.Tools.Validation.Framework;

/// <summary>
/// Executes the registered <see cref="IValidationRule"/>s over a materialized snapshot and
/// aggregates the findings into a consolidated <see cref="IValidationResult"/>. Rules are
/// discovered through the container (constructor injection of <c>IEnumerable&lt;IValidationRule&gt;</c>)
/// and may be composed or extended without changing the engine.
/// </summary>
public interface IValidationEngine
{
    /// <summary>The rules registered with the engine, in registration order.</summary>
    IReadOnlyList<IValidationRule> Rules { get; }

    /// <summary>Executes every rule, times each one, isolates per-rule failures and returns the
    /// raw findings with rule-accounting. Cancellation is honoured between rules.</summary>
    /// <param name="data">The materialized snapshot of the drawing and its domain objects.</param>
    /// <param name="context">Correlation/session identity, logger and cancellation.</param>
    RuleExecutionResult ExecuteRules(ValidationData data, IValidationContext context);

    /// <summary>Aggregates executed findings into categories, a severity/rule summary and
    /// top-level recommendations.</summary>
    /// <param name="execution">The outcome of <see cref="ExecuteRules"/>.</param>
    /// <param name="objectCount">The total number of domain objects inspected.</param>
    IValidationResult Aggregate(RuleExecutionResult execution, int objectCount);
}
