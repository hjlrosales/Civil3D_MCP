using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Sdk.Discovery;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>Handles <c>tools/list</c>: returns the full tool catalog as a manifest.</summary>
public sealed class ListToolsHandler : IProtocolHandler
{
    private readonly IToolCatalog _catalog;

    /// <summary>Creates the handler.</summary>
    public ListToolsHandler(IToolCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public string Method => ProtocolConstants.ToolsList;

    /// <inheritdoc />
    public Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken)
        => Task.FromResult<object?>(new Manifest
        {
            SchemaVersion = 1,
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Tools = _catalog.Manifests,
        });
}
