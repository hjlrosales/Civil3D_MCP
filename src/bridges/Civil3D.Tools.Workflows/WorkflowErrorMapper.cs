using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Workflows;

namespace Civil3D.Tools.Workflows;

/// <summary>
/// Translates workflow failures into stable protocol errors so raw exceptions never cross the
/// pipe. <see cref="WorkflowException"/> codes map 1:1 to <c>ErrorCode</c> values; a
/// <see cref="DomainException"/> thrown by a handler/step maps to the same codes the read-only
/// and editing tools use.
/// </summary>
internal static class WorkflowErrorMapper
{
    internal static BridgeException Map(ToolExecutionContext context, WorkflowException ex)
        => ex.Code switch
        {
            WorkflowErrorCode.ValidationFailed => new BridgeException(
                ErrorCode.E_VALIDATION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            WorkflowErrorCode.PermissionDenied => new BridgeException(
                ErrorCode.E_PERMISSION_DENIED, ex.Message, context.CorrelationId, context.SessionId),
            WorkflowErrorCode.Cancelled => new BridgeException(
                ErrorCode.E_CANCELLED, ex.Message, context.CorrelationId, context.SessionId),
            WorkflowErrorCode.Timeout => new BridgeException(
                ErrorCode.E_TIMEOUT, ex.Message, context.CorrelationId, context.SessionId),
            WorkflowErrorCode.InvalidParameters => new BridgeException(
                ErrorCode.E_INVALID_PARAMETERS, ex.Message, context.CorrelationId, context.SessionId),
            _ => new BridgeException(
                ErrorCode.E_INTERNAL, "An internal error occurred while executing the workflow.",
                context.CorrelationId, context.SessionId, ex),
        };

    internal static BridgeException Map(ToolExecutionContext context, DomainException ex)
        => ex.Code switch
        {
            DomainErrorCode.NoActiveDocument => new BridgeException(
                ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.Message, context.CorrelationId, context.SessionId),
            DomainErrorCode.EntityNotFound => new BridgeException(
                ErrorCode.E_OBJECT_NOT_FOUND, ex.Message, context.CorrelationId, context.SessionId),
            DomainErrorCode.TransactionFailed => new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            _ => new BridgeException(
                ErrorCode.E_INTERNAL, "An internal error occurred while executing the workflow.",
                context.CorrelationId, context.SessionId, ex),
        };
}
