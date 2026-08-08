using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Tools;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Handles <c>tools/execute</c>: validates the tool exists and its arguments match the input
/// schema, then forwards execution to the <see cref="IToolExecutor"/> (the dispatcher).
/// Returns the executor's envelope directly.
/// </summary>
public sealed class ExecuteToolHandler : IProtocolHandler
{
    private readonly IToolCatalog _catalog;
    private readonly IToolExecutor _executor;
    private readonly ILogger<ExecuteToolHandler> _logger;

    /// <summary>Creates the handler.</summary>
    public ExecuteToolHandler(IToolCatalog catalog, IToolExecutor executor, ILogger<ExecuteToolHandler> logger)
    {
        _catalog = catalog;
        _executor = executor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Method => ProtocolConstants.ToolsExecute;

    /// <inheritdoc />
    public async Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken)
    {
        ExecuteToolRequest? request = parameters is { ValueKind: JsonValueKind.Object }
            ? parameters.Value.Deserialize<ExecuteToolRequest>(SharedJson.Options)
            : null;

        if (request is null || string.IsNullOrWhiteSpace(request.Tool))
        {
            throw new ProtocolException("tools/execute requires an object payload with a 'tool' name.");
        }

        if (!_catalog.TryGetTool(request.Tool, out ITool tool))
        {
            throw new BridgeException(ErrorCode.E_OBJECT_NOT_FOUND, $"Unknown tool '{request.Tool}'.");
        }

        ValidateArguments(request.Tool, request.Arguments);

        ToolManifest? manifest = _catalog.GetManifest(request.Tool);
        var invocation = new ToolInvocation
        {
            ToolName = request.Tool,
            Parameters = request.Arguments,
            CorrelationId = context.CorrelationId,
            SessionId = context.SessionId,
            TimeoutMilliseconds = request.TimeoutMs ?? manifest?.TimeoutMilliseconds,
        };

        ResponseEnvelope response = await _executor.ExecuteAsync(invocation, cancellationToken);
        _logger.LogInformation(
            "Tool {Tool} returned {Success} ({ErrorCode}) in {ExecutionTime} ms (correlation {CorrelationId}).",
            request.Tool, response.Success, response.ErrorCode, response.ExecutionTime, context.CorrelationId);
        return response;
    }

    private void ValidateArguments(string toolName, JsonElement? arguments)
    {
        JsonSchema? schema = _catalog.GetInputSchema(toolName);
        if (schema is null || arguments is not { ValueKind: JsonValueKind.Object })
        {
            return;
        }

        ICollection<NJsonSchema.Validation.ValidationError> errors = schema.Validate(arguments.Value.GetRawText());
        if (errors.Count > 0)
        {
            string detail = string.Join(
                "; ",
                errors.Take(3).Select(e => $"{e.Path} {e.Kind}"));
            throw new BridgeException(ErrorCode.E_INVALID_PARAMETERS, $"Invalid parameters for '{toolName}': {detail}");
        }
    }
}
