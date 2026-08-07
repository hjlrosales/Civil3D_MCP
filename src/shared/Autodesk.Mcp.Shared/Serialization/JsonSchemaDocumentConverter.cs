using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Schemas;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>Serializes a <see cref="JsonSchemaDocument"/> as its raw JSON object.</summary>
public sealed class JsonSchemaDocumentConverter : JsonConverter<JsonSchemaDocument>
{
    /// <inheritdoc />
    public override JsonSchemaDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonNode? node = JsonNode.Parse(ref reader);
        if (node is JsonObject obj)
        {
            return new JsonSchemaDocument(obj);
        }

        throw new JsonException("A JSON Schema document must be a JSON object.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonSchemaDocument value, JsonSerializerOptions options)
        => value.Root.WriteTo(writer);
}
