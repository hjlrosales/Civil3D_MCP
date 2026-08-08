namespace Civil3D.Domain.Commands;

/// <summary>
/// Stable error codes produced by the command framework. The command dispatcher, validators and
/// transaction pipeline throw <see cref="CommandException"/> carrying one of these codes; the tool
/// layer maps each code to a protocol <c>ErrorCode</c> (<c>E_VALIDATION_FAILED</c>,
/// <c>E_PERMISSION_DENIED</c>, <c>E_CONFIRMATION_REQUIRED</c>, …) so raw exceptions never cross
/// the pipe.
/// </summary>
public enum CommandErrorCode
{
    /// <summary>One or more validators rejected the command.</summary>
    ValidationFailed,

    /// <summary>The caller lacks the permission the command requires.</summary>
    PermissionDenied,

    /// <summary>The command requires user confirmation that was not granted.</summary>
    ConfirmationRequired,

    /// <summary>No drawing/document is currently open.</summary>
    NoActiveDocument,

    /// <summary>The write transaction failed and was rolled back.</summary>
    TransactionFailed,

    /// <summary>The write transaction exceeded its timeout and was rolled back.</summary>
    TransactionTimeout,

    /// <summary>A transaction was already active; nested transactions are not supported.</summary>
    TransactionAlreadyActive,

    /// <summary>The operation was cancelled.</summary>
    Cancelled,

    /// <summary>A requested entity could not be found.</summary>
    ObjectNotFound,

    /// <summary>Input parameters were structurally invalid.</summary>
    InvalidParameters,

    /// <summary>An unexpected internal failure occurred.</summary>
    Internal,
}
