using Civil3D.Domain.Commands;

namespace Civil3D.Domain.Workflows;

/// <summary>Published when the dispatcher starts executing a workflow.</summary>
/// <param name="WorkflowName">The workflow name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
public sealed record WorkflowStarted(string WorkflowName, string CorrelationId, string? SessionId) : IDomainEvent;

/// <summary>Published when a workflow completed successfully.</summary>
/// <param name="WorkflowName">The workflow name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
/// <param name="ExecutionTimeMs">Wall-clock execution time of the workflow.</param>
/// <param name="Elapsed">The workflow duration reported by its result.</param>
public sealed record WorkflowCompleted(
    string WorkflowName, string CorrelationId, string? SessionId, long ExecutionTimeMs, TimeSpan Elapsed) : IDomainEvent;

/// <summary>Published when a workflow failed. The reason never carries exception details.</summary>
/// <param name="WorkflowName">The workflow name.</param>
/// <param name="CorrelationId">Correlation of the originating request.</param>
/// <param name="SessionId">Session of the originating request, when present.</param>
/// <param name="ErrorCode">The stable <see cref="WorkflowErrorCode"/> name.</param>
/// <param name="Message">Optional human-readable failure description.</param>
public sealed record WorkflowFailed(
    string WorkflowName, string CorrelationId, string? SessionId, string ErrorCode, string? Message) : IDomainEvent;
