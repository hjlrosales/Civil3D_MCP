namespace Civil3D.Domain.Workflows;

/// <summary>
/// Stable error codes produced by the workflow framework. The dispatcher, validators and step
/// executor throw <see cref="WorkflowException"/> carrying one of these codes; the tool layer
/// maps each code to a protocol <c>ErrorCode</c> (<c>E_VALIDATION_FAILED</c>,
/// <c>E_PERMISSION_DENIED</c>, <c>E_CANCELLED</c>, <c>E_TIMEOUT</c>, …) so raw exceptions never
/// cross the pipe.
/// </summary>
public enum WorkflowErrorCode
{
    /// <summary>One or more validators rejected the workflow.</summary>
    ValidationFailed,

    /// <summary>The caller lacks the permission the workflow requires.</summary>
    PermissionDenied,

    /// <summary>The workflow was cancelled by the caller.</summary>
    Cancelled,

    /// <summary>The workflow exceeded its execution timeout.</summary>
    Timeout,

    /// <summary>A workflow step failed.</summary>
    StepFailed,

    /// <summary>Input parameters were structurally invalid.</summary>
    InvalidParameters,

    /// <summary>An unexpected internal failure occurred.</summary>
    Internal,
}
