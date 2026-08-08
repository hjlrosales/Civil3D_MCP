using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Errors;

namespace Civil3D.Tools.Commands;

/// <summary>
/// Translates framework failures into stable protocol errors so raw exceptions never cross the
/// pipe. <see cref="CommandException"/> codes map 1:1 to <c>ErrorCode</c> values; a
/// <see cref="DomainException"/> thrown by a handler/repository maps to the same codes the
/// read-only tools use.
/// </summary>
internal static class CommandErrorMapper
{
    internal static BridgeException Map(ToolExecutionContext context, CommandException ex)
        => ex.Code switch
        {
            CommandErrorCode.ValidationFailed => new BridgeException(
                ErrorCode.E_VALIDATION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.PermissionDenied => new BridgeException(
                ErrorCode.E_PERMISSION_DENIED, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.ConfirmationRequired => new BridgeException(
                ErrorCode.E_CONFIRMATION_REQUIRED, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.NoActiveDocument => new BridgeException(
                ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.TransactionFailed or CommandErrorCode.TransactionAlreadyActive => new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.TransactionTimeout => new BridgeException(
                ErrorCode.E_TIMEOUT, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.Cancelled => new BridgeException(
                ErrorCode.E_CANCELLED, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.ObjectNotFound => new BridgeException(
                ErrorCode.E_OBJECT_NOT_FOUND, ex.Message, context.CorrelationId, context.SessionId),
            CommandErrorCode.InvalidParameters => new BridgeException(
                ErrorCode.E_INVALID_PARAMETERS, ex.Message, context.CorrelationId, context.SessionId),
            _ => new BridgeException(
                ErrorCode.E_INTERNAL, "An internal error occurred while executing the command.",
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
            DomainErrorCode.DuplicateName or DomainErrorCode.InvalidName => new BridgeException(
                ErrorCode.E_VALIDATION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            _ => new BridgeException(
                ErrorCode.E_INTERNAL, "An internal error occurred while executing the command.",
                context.CorrelationId, context.SessionId, ex),
        };
}
