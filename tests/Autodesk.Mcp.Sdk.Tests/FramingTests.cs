using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Shared.Errors;
using Xunit;

namespace Autodesk.Mcp.Sdk.Tests;

/// <summary>NDJSON framing over a stream.</summary>
public class FramingTests
{
    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        stream.Position = 0;
        using var reader = new StreamReader(stream);

        stream.Position = 0;
        await NdjsonProtocol.WriteLineAsync(writer, "{\"a\":1}", CancellationToken.None);
        stream.Position = 0;

        string? line = await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None);
        Assert.Equal("{\"a\":1}", line);
    }

    [Fact]
    public async Task MultipleLines_AreReadInOrder()
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        await NdjsonProtocol.WriteLineAsync(writer, "{\"id\":1}", CancellationToken.None);
        await NdjsonProtocol.WriteLineAsync(writer, "{\"id\":2}", CancellationToken.None);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        Assert.Equal("{\"id\":1}", await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None));
        Assert.Equal("{\"id\":2}", await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None));
        Assert.Null(await NdjsonProtocol.ReadLineAsync(reader, CancellationToken.None));
    }

    [Fact]
    public async Task OversizedWrite_ThrowsProtocolException()
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        string huge = new('x', NdjsonProtocol.MaxMessageLength + 1);

        await Assert.ThrowsAsync<ProtocolException>(
            () => NdjsonProtocol.WriteLineAsync(writer, huge, CancellationToken.None));
    }
}
