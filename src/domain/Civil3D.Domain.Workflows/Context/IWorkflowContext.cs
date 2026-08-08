using Civil3D.Domain.Commands;
using Microsoft.Extensions.Logging;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// Per-execution context handed to validators, handlers and steps: correlation and session
/// identifiers, the effective cancellation token, progress reporting, a logger, the container
/// (for domain services and repositories used by steps), workflow configuration and the granted
/// permission level. Contains no Autodesk types; a fresh instance is created for every dispatch.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>The workflow name.</summary>
    string WorkflowName { get; }

    /// <summary>Correlation identifier of the originating request.</summary>
    string CorrelationId { get; }

    /// <summary>Session identifier, when the request carried one.</summary>
    string? SessionId { get; }

    /// <summary>Effective cancellation token (client cancellation, timeout, shutdown).</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Progress reporter for the workflow.</summary>
    IWorkflowProgress Progress { get; }

    /// <summary>Logger scoped to the originating request.</summary>
    ILogger Logger { get; }

    /// <summary>
    /// The container, used by steps to resolve domain services and repositories. Handlers should
    /// prefer constructor injection; steps are composed by the workflow definition and resolve
    /// their dependencies lazily from here.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>Workflow configuration (key/value settings), empty when none supplied.</summary>
    IReadOnlyDictionary<string, string> Configuration { get; }

    /// <summary>The permission granted to the caller; the pipeline rejects workflows above this level.</summary>
    CommandPermission EffectivePermission { get; }

    /// <summary>UTC timestamp when execution started.</summary>
    DateTimeOffset StartedAtUtc { get; }
}

/// <summary>The standard immutable implementation of <see cref="IWorkflowContext"/>.</summary>
public sealed record WorkflowContext(
    string WorkflowName,
    string CorrelationId,
    string? SessionId,
    CancellationToken CancellationToken,
    IWorkflowProgress Progress,
    ILogger Logger,
    IServiceProvider Services,
    IReadOnlyDictionary<string, string> Configuration,
    CommandPermission EffectivePermission,
    DateTimeOffset StartedAtUtc) : IWorkflowContext;
