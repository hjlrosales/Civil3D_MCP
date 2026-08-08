using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Sdk.Tools;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Executes tool invocations and returns the standard response envelope. Implementations marshal
/// Autodesk-touching tools onto the host application context.
/// </summary>
public interface IToolExecutor
{
    /// <summary>Executes a tool invocation.</summary>
    /// <param name="invocation">The invocation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ResponseEnvelope> ExecuteAsync(ToolInvocation invocation, CancellationToken cancellationToken);
}
