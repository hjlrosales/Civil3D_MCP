using Autodesk.Mcp.Shared.Errors;

namespace Autodesk.Mcp.Sdk.Communication;

/// <summary>
/// Newline-delimited JSON framing (NDJSON) for the named pipe (AD-02). One UTF-8 JSON object per
/// line; JSON never contains raw newlines, so framing is unambiguous and trivially debuggable.
/// </summary>
public static class NdjsonProtocol
{
    /// <summary>Hard guard against oversized wire messages.</summary>
    public const int MaxMessageLength = 4 * 1024 * 1024;

    /// <summary>Reads the next JSON line; returns null at end of stream.</summary>
    /// <param name="reader">The stream reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line = await reader.ReadLineAsync(cancellationToken);
        if (line is not null && line.Length > MaxMessageLength)
        {
            throw new ProtocolException("A wire message exceeded the maximum allowed length.");
        }

        return line;
    }

    /// <summary>Writes one JSON line (the writer must have AutoFlush enabled).</summary>
    /// <param name="writer">The stream writer.</param>
    /// <param name="json">The JSON text; must not contain raw newlines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteLineAsync(StreamWriter writer, string json, CancellationToken cancellationToken)
    {
        if (json.Length > MaxMessageLength)
        {
            throw new ProtocolException("A wire message exceeded the maximum allowed length.");
        }

        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}
