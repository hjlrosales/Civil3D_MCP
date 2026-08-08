using System.Diagnostics;
using System.Text.Json;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Microsoft.Extensions.Logging;

namespace Civil3D.Tools.Abstractions;

/// <summary>
/// Common base for every Civil 3D tool. Provides the standard responsibilities shared by all tools:
/// <list type="bullet">
/// <item>access to the active drawing through the injected <see cref="ICivil3DSession"/> (document availability validation, no duplicate Autodesk code);</item>
/// <item>access to the per-invocation execution context and its scoped logger;</item>
/// <item>standard exception handling: <c>BridgeException</c> and cancellation pass through unchanged, every other failure is logged and mapped to <c>E_INTERNAL</c> so raw Autodesk exceptions never cross the pipe;</item>
/// <item>execution logging (tool, drawing name, execution time, correlation/session ids, result size).</item>
/// </list>
/// Database, editor and transaction access are intentionally not typed members of this class: they are
/// provided by Autodesk-free service contracts (<see cref="ICivil3DSession"/>, <see cref="IDrawingStatisticsService"/>,
/// and future domain services) whose real implementations live in the tool assemblies. That keeps this base
/// fully unit-testable without Civil 3D.
/// </summary>
/// <typeparam name="TIn">Input DTO; must be a class with a parameterless constructor.</typeparam>
/// <typeparam name="TOut">Output DTO (or plain class).</typeparam>
public abstract class Civil3DToolBase<TIn, TOut> : ToolBase<TIn, TOut>
    where TIn : class, new()
    where TOut : class
{
    /// <summary>Creates the tool with its session dependency.</summary>
    /// <param name="session">Session contract used to resolve and validate the active drawing.</param>
    protected Civil3DToolBase(ICivil3DSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>The session contract used to resolve the active drawing.</summary>
    protected ICivil3DSession Session { get; }

    /// <inheritdoc />
    /// <remarks>
    /// All Civil 3D tools touch Autodesk APIs and must run on the application context. Sealed so a
    /// future tool cannot accidentally disable marshaling.
    /// </remarks>
    public sealed override bool RequiresApplicationContext => true;

    /// <inheritdoc />
    protected override async Task<TOut> ExecuteCoreAsync(TIn input, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        TOut result;
        try
        {
            result = await ExecuteToolCoreAsync(input, context, cancellationToken);
            timer.Stop();
        }
        catch (BridgeException)
        {
            throw; // Stable wire code already chosen; never remap.
        }
        catch (OperationCanceledException)
        {
            throw; // The dispatcher maps timeouts/cancellation to the correct envelope.
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                ex,
                "Tool {Tool} failed (correlation {CorrelationId}, session {SessionId}).",
                context.ToolName, context.CorrelationId, context.SessionId);
            throw new BridgeException(
                ErrorCode.E_INTERNAL,
                "An internal error occurred while executing the tool.",
                context.CorrelationId,
                context.SessionId,
                ex);
        }

        // Logging happens outside the error mapping so a logging failure can never turn a
        // successful run into an E_INTERNAL response.
        try
        {
            LogCompleted(context, timer.ElapsedMilliseconds, result);
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to log tool {Tool} completion (correlation {CorrelationId}).",
                context.ToolName, context.CorrelationId);
        }

        return result;
    }

    /// <summary>
    /// Executes the tool with strongly typed input. Implementations access the active drawing via
    /// <see cref="RequireActiveDrawing"/> and return an immutable DTO.
    /// </summary>
    /// <param name="input">Bound input parameters.</param>
    /// <param name="context">Per-invocation execution context.</param>
    /// <param name="cancellationToken">Effective cancellation token.</param>
    protected abstract Task<TOut> ExecuteToolCoreAsync(TIn input, ToolExecutionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the active drawing snapshot or throws <c>E_NO_ACTIVE_DOCUMENT</c>.
    /// </summary>
    /// <param name="context">Per-invocation execution context (carries correlation/session ids for the error).</param>
    protected ActiveDrawing RequireActiveDrawing(ToolExecutionContext context)
        => Session.GetActiveDrawing()
           ?? throw new BridgeException(
               ErrorCode.E_NO_ACTIVE_DOCUMENT,
               "No active document is available to operate on.",
               context.CorrelationId,
               context.SessionId);

    private void LogCompleted(ToolExecutionContext context, long executionTimeMs, TOut result)
    {
        string drawingName = Session.GetActiveDrawing()?.DrawingName ?? "<none>";
        int resultSize = result is null
            ? 0
            : JsonSerializer.SerializeToElement(result, SharedJson.Options).GetRawText().Length;

        context.Logger.LogInformation(
            "Tool {Tool} executed on drawing '{DrawingName}' in {ExecutionTime} ms with a result of {ResultSize} bytes (correlation {CorrelationId}, session {SessionId}).",
            context.ToolName, drawingName, executionTimeMs, resultSize, context.CorrelationId, context.SessionId);
    }
}
