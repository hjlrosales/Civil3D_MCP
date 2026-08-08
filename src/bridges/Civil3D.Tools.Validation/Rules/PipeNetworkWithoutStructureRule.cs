using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;

namespace Civil3D.Tools.Validation.Rules;

/// <summary>
/// Finds pipe networks that contain no structures. A network without structures is often
/// incomplete, though some all-pipe networks are valid. Warning severity.
/// </summary>
public sealed class PipeNetworkWithoutStructureRule : IValidationRule
{
    /// <inheritdoc />
    public string Name => "pipe-networks-without-structures";

    /// <inheritdoc />
    public string Category => "Pipe Networks";

    /// <inheritdoc />
    public IReadOnlyList<ValidationIssue> Evaluate(ValidationData data, IValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();

        foreach (var network in data.PipeNetworks.Where(n => n.StructureCount == 0))
        {
            issues.Add(new ValidationIssue
            {
                Code = "PIPE_NETWORK_WITHOUT_STRUCTURES",
                Rule = "pipe-networks-without-structures",
                Severity = ValidationSeverity.Warning,
                Category = Category,
                Title = $"Pipe network '{network.Name}' has no structures",
                Description = $"Pipe network '{network.Name}' contains no structures.",
                SuggestedAction = "Verify the network is complete; add structures or confirm it is intentionally pipes-only.",
                RelatedObject = network.Name,
            });
        }

        return issues;
    }
}
