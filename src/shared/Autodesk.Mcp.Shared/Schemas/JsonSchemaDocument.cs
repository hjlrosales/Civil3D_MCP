using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Serialization;

namespace Autodesk.Mcp.Shared.Schemas;

/// <summary>
/// An opaque JSON Schema document embedded in a <see cref="Dtos.ToolManifest"/>. Schemas are
/// generated from DTO types by NJsonSchema at bridge startup; this wrapper keeps them as raw JSON
/// so the contract never depends on the generator.
/// </summary>
[JsonConverter(typeof(JsonSchemaDocumentConverter))]
public sealed record JsonSchemaDocument
{
    /// <summary>Creates a schema document wrapping the given JSON object.</summary>
    /// <param name="root">The schema root object; must not be null.</param>
    public JsonSchemaDocument(JsonObject root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>The schema root object. Treat as immutable; never mutate in place.</summary>
    public JsonObject Root { get; }

    /// <summary>An empty schema document (no constraints), the default for optional schemas.</summary>
    public static JsonSchemaDocument Empty { get; } = new(new JsonObject());

    /// <summary>Serializes the schema to its JSON string form.</summary>
    public string ToJsonString() => Root.ToJsonString();

    /// <summary>Converts the schema to a JSON element.</summary>
    public JsonElement ToJsonElement() => JsonSerializer.SerializeToElement(Root);

    /// <summary>Parses a schema from its JSON string form.</summary>
    /// <param name="json">The schema JSON.</param>
    public static JsonSchemaDocument FromJson(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("The schema JSON is empty.");
        return node is JsonObject obj
            ? new JsonSchemaDocument(obj)
            : throw new JsonException("A JSON Schema document must be a JSON object.");
    }

    /// <summary>Wraps an existing JSON element as a schema document.</summary>
    /// <param name="element">The schema element; must be an object.</param>
    public static JsonSchemaDocument FromJsonElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A JSON Schema document must be a JSON object.");
        }

        var node = JsonNode.Parse(element.GetRawText());
        return new JsonSchemaDocument((node as JsonObject)!);
    }
}
