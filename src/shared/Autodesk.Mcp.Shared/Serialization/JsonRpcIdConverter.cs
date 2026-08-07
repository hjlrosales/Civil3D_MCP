using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>
/// Serializes a <see cref="JsonRpcId"/> as a JSON-RPC 2.0 id: a number, a string, or omitted/null
/// when the id is null (notifications).
/// </summary>
public sealed class JsonRpcIdConverter : JsonConverter<JsonRpcId>
{
    /// <inheritdoc />
    public override JsonRpcId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return JsonRpcId.Null;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out long number) ? new JsonRpcId(number) : JsonRpcId.Null;
            case JsonTokenType.String:
                return new JsonRpcId(reader.GetString() ?? string.Empty);
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' while reading a JSON-RPC id.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonRpcId value, JsonSerializerOptions options)
    {
        if (value.IsNull)
        {
            writer.WriteNullValue();
        }
        else if (value.AsNumber() is long number)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(value.AsString());
        }
    }
}
