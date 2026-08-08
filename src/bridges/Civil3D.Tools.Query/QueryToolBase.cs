using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Civil3D.Domain.Errors;
using Civil3D.Domain.Query;
using Civil3D.Tools.Abstractions;

namespace Civil3D.Tools.Query;

/// <summary>
/// Base for every read-only query tool. Extends <see cref="Civil3DToolBase{TIn,TOut}"/> with the
/// standard domain translation: <see cref="QueryException"/> becomes <c>E_INVALID_PARAMETERS</c>
/// and <see cref="DomainException"/> codes map to the protocol error codes
/// (<c>NoActiveDocument</c> → <c>E_NO_ACTIVE_DOCUMENT</c>, <c>EntityNotFound</c> →
/// <c>E_OBJECT_NOT_FOUND</c>, <c>TransactionFailed</c> → <c>E_TRANSACTION_FAILED</c>, otherwise
/// <c>E_INTERNAL</c>). Raw Autodesk exceptions never cross the pipe.
/// </summary>
/// <typeparam name="TIn">Input DTO; must be a class with a parameterless constructor.</typeparam>
/// <typeparam name="TOut">Output DTO.</typeparam>
public abstract class QueryToolBase<TIn, TOut> : Civil3DToolBase<TIn, TOut>
    where TIn : class, new()
    where TOut : class
{
    /// <summary>Creates the tool with its session dependency.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    protected QueryToolBase(ICivil3DSession session)
        : base(session)
    {
    }

    /// <summary>
    /// Executes a domain query, translating <see cref="QueryException"/> and
    /// <see cref="DomainException"/> into the matching <see cref="BridgeException"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">Per-invocation execution context.</param>
    /// <param name="query">The domain service call.</param>
    protected static T RunQuery<T>(ToolExecutionContext context, Func<T> query)
    {
        try
        {
            return query();
        }
        catch (QueryException ex)
        {
            throw new BridgeException(
                ErrorCode.E_INVALID_PARAMETERS,
                ex.Message,
                context.CorrelationId,
                context.SessionId);
        }
        catch (DomainException ex)
        {
            throw Translate(context, ex);
        }
    }

    private static BridgeException Translate(ToolExecutionContext context, DomainException ex)
        => ex.Code switch
        {
            DomainErrorCode.NoActiveDocument => new BridgeException(
                ErrorCode.E_NO_ACTIVE_DOCUMENT, ex.Message, context.CorrelationId, context.SessionId),
            DomainErrorCode.EntityNotFound => new BridgeException(
                ErrorCode.E_OBJECT_NOT_FOUND, ex.Message, context.CorrelationId, context.SessionId),
            DomainErrorCode.TransactionFailed => new BridgeException(
                ErrorCode.E_TRANSACTION_FAILED, ex.Message, context.CorrelationId, context.SessionId),
            _ => new BridgeException(
                ErrorCode.E_INTERNAL, "An internal error occurred while executing the tool.",
                context.CorrelationId, context.SessionId, ex),
        };
}
