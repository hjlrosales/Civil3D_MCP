using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Sdk.Communication;

/// <summary>
/// Wraps one named-pipe stream with NDJSON read/write and connection-scoped correlation.
/// One instance per accepted connection; writes are serialized for concurrent use.
/// </summary>
public sealed class PipeConnection : IAsyncDisposable
{
    private readonly PipeStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>Unique id of this connection, used in logs.</summary>
    public Guid ConnectionId { get; } = Guid.NewGuid();

    /// <summary>Creates a connection over the given pipe stream.</summary>
    /// <param name="stream">A connected pipe stream.</param>
    public PipeConnection(PipeStream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: false);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    /// <summary>Reads the next request envelope; returns null when the client closed the connection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RequestEnvelope?> ReceiveAsync(CancellationToken cancellationToken)
    {
        string? line = await NdjsonProtocol.ReadLineAsync(_reader, cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RequestEnvelope>(line, SharedJson.Options);
        }
        catch (JsonException ex)
        {
            throw new ProtocolException("The received message is not a valid request envelope.", ConnectionId.ToString("D"), ex);
        }
    }

    /// <summary>Serializes a payload and writes it as one JSON line.</summary>
    /// <param name="payload">The payload to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(payload, payload.GetType(), SharedJson.Options);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await NdjsonProtocol.WriteLineAsync(_writer, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _writeGate.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
