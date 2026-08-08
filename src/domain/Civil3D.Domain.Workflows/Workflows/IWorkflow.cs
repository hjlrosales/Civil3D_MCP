using Civil3D.Domain.Commands;

namespace Civil3D.Domain.Workflows;

/// <summary>
/// A workflow: a named, permission-checked, optionally timed-out collection of
/// <see cref="IWorkflowStep"/>s that coordinates domain services to produce a typed result.
/// Workflows are data-and-steps definitions created by the tool layer; execution is owned by
/// <see cref="IWorkflowDispatcher"/>. Contains no Autodesk types.
/// </summary>
public interface IWorkflow
{
    /// <summary>The stable workflow name (used in logs, progress and events).</summary>
    string Name { get; }

    /// <summary>The permission required to execute; the pipeline rejects workflows above the granted level.</summary>
    CommandPermission RequiredPermission { get; }

    /// <summary>Optional execution timeout; when null the dispatcher applies its default.</summary>
    TimeSpan? Timeout { get; }

    /// <summary>The ordered steps composing this workflow.</summary>
    IReadOnlyList<IWorkflowStep> Steps { get; }
}

/// <summary>
/// A workflow producing a typed result.
/// </summary>
/// <typeparam name="TResult">The result type produced by the workflow.</typeparam>
public interface IWorkflow<out TResult> : IWorkflow
{
}
