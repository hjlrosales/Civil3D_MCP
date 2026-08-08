namespace Civil3D.Domain.Workflows;

/// <summary>
/// The common shape of every workflow result: success flag, optional error code and message, and
/// execution timing. Failures are still reported by throwing <see cref="WorkflowException"/>;
/// the result shape exists for serialization, logging and future non-throwing workflows.
/// </summary>
public interface IWorkflowResult
{
    /// <summary>True when the workflow completed successfully.</summary>
    bool Success { get; }

    /// <summary>The stable error code, when the result is a failure.</summary>
    string? ErrorCode { get; }

    /// <summary>Optional human-readable message.</summary>
    string? Message { get; }

    /// <summary>UTC timestamp when execution started.</summary>
    DateTimeOffset StartedAtUtc { get; }

    /// <summary>UTC timestamp when execution finished.</summary>
    DateTimeOffset FinishedAtUtc { get; }

    /// <summary>Total execution duration.</summary>
    TimeSpan Elapsed { get; }
}

/// <summary>The typed result of a workflow execution.</summary>
/// <typeparam name="TResult">The workflow result data type.</typeparam>
public sealed record WorkflowResult<TResult>(
    TResult Data,
    bool Success,
    string? ErrorCode,
    string? Message,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc) : IWorkflowResult
{
    /// <inheritdoc />
    public TimeSpan Elapsed => FinishedAtUtc - StartedAtUtc;
}
