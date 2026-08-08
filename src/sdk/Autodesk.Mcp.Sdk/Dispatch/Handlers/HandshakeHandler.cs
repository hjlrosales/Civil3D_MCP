using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;
using Autodesk.Mcp.Sdk.Hosting;
using Microsoft.Extensions.Logging;

namespace Autodesk.Mcp.Sdk.Dispatch;

/// <summary>
/// Handles <c>handshake</c>: negotiates the protocol version, creates a session and returns the
/// bridge information. Refuses clients whose protocol major version is incompatible.
/// </summary>
public sealed class HandshakeHandler : IProtocolHandler
{
    private readonly IEndpointInfoProvider _info;
    private readonly SessionStore _sessions;
    private readonly ILogger<HandshakeHandler> _logger;

    /// <summary>Creates the handler.</summary>
    public HandshakeHandler(IEndpointInfoProvider info, SessionStore sessions, ILogger<HandshakeHandler> logger)
    {
        _info = info;
        _sessions = sessions;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Method => ProtocolConstants.Handshake;

    /// <inheritdoc />
    public Task<object?> HandleAsync(JsonElement? parameters, RpcContext context, CancellationToken cancellationToken)
    {
        HandshakeRequest? request = parameters is { ValueKind: JsonValueKind.Object }
            ? parameters.Value.Deserialize<HandshakeRequest>(SharedJson.Options)
            : null;

        if (request is null || string.IsNullOrWhiteSpace(request.ClientName))
        {
            throw new ProtocolException("A handshake request requires a 'clientName'.");
        }

        VersionInformation current = ProtocolConstants.CurrentProtocolVersion;
        VersionInformation client = request.ProtocolVersion == VersionInformation.Empty ? current : request.ProtocolVersion;
        if (client.Major != current.Major)
        {
            throw new ProtocolException(
                $"Unsupported protocol version {client}. This bridge speaks protocol {current.Major}.x.");
        }

        SessionInformation session = _sessions.Create(request.ClientName, request.ClientVersion);
        _logger.LogInformation(
            "Handshake accepted: client {ClientName} {ClientVersion}, session {SessionId}, protocol {Protocol}.",
            request.ClientName, request.ClientVersion, session.SessionId, client);

        return Task.FromResult<object?>(new HandshakeResponse
        {
            ProtocolVersion = client <= current ? client : current,
            SessionId = session.SessionId,
            Bridge = _info.GetBridgeInformation(),
        });
    }
}
