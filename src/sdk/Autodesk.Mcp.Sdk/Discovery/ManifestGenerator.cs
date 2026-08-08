using System.Reflection;
using System.Text.Json;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Dtos;
using Autodesk.Mcp.Shared.Schemas;
using Autodesk.Mcp.Sdk.Tools;
using NJsonSchema;
using NJsonSchema.Generation;

namespace Autodesk.Mcp.Sdk.Discovery;

/// <summary>
/// Builds <see cref="ToolManifest"/> documents from tool types using reflection over
/// <see cref="McpToolAttribute"/> and NJsonSchema over the tool's input/output DTOs.
/// No manifest is ever hand-written.
/// </summary>
public sealed class ManifestGenerator
{
    private static readonly JsonSchemaGeneratorSettings SchemaSettings = new SystemTextJsonSchemaGeneratorSettings
    {
        SchemaType = SchemaType.JsonSchema,
        SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        },
        DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
    };

    /// <summary>Builds the full manifest for a tool instance.</summary>
    /// <param name="tool">The tool instance.</param>
    public ToolManifest Generate(ITool tool) => Generate(tool.GetType());

    /// <summary>Builds the full manifest for a tool type without instantiating it.</summary>
    /// <param name="toolType">The tool type; must derive from <see cref="ToolBase{TIn,TOut}"/>.</param>
    public ToolManifest Generate(Type toolType)
    {
        McpToolAttribute attribute = toolType.GetCustomAttribute<McpToolAttribute>(inherit: false)
            ?? throw new InvalidOperationException($"Tool '{toolType.FullName}' is missing the [McpTool] attribute.");

        (Type input, Type output) = GetToolIoTypes(toolType);

        return new ToolManifest
        {
            Name = attribute.Name,
            DisplayName = attribute.DisplayName,
            Description = attribute.Description,
            Category = attribute.Category,
            Permission = attribute.Permission,
            Risk = attribute.Risk,
            Version = VersionInformation.TryParse(attribute.Version, out VersionInformation version) ? version : new VersionInformation(1, 0, 0),
            TimeoutMilliseconds = attribute.TimeoutMilliseconds,
            SupportsProgress = attribute.SupportsProgress,
            SupportsCancellation = attribute.SupportsCancellation,
            SupportsStreaming = attribute.SupportsStreaming,
            InputSchema = GenerateSchema(input),
            OutputSchema = GenerateSchema(output),
            Tags = attribute.Tags,
            Deprecated = attribute.Deprecated,
        };
    }

    /// <summary>Resolves the input DTO type of a tool type.</summary>
    /// <param name="toolType">The tool type.</param>
    public static Type GetInputType(Type toolType) => GetToolIoTypes(toolType).Input;

    /// <summary>Resolves the output DTO type of a tool type.</summary>
    /// <param name="toolType">The tool type.</param>
    public static Type GetOutputType(Type toolType) => GetToolIoTypes(toolType).Output;

    /// <summary>Generates the JSON Schema for a DTO type (used for request validation).</summary>
    /// <param name="type">The DTO type.</param>
    public JsonSchema GenerateJsonSchema(Type type) => JsonSchema.FromType(type, SchemaSettings);

    private static (Type Input, Type Output) GetToolIoTypes(Type toolType)
    {
        for (Type? current = toolType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ToolBase<,>))
            {
                Type[] arguments = current.GetGenericArguments();
                return (arguments[0], arguments[1]);
            }
        }

        throw new InvalidOperationException($"Tool '{toolType.FullName}' must derive from ToolBase<TIn,TOut>.");
    }

    private static JsonSchemaDocument GenerateSchema(Type type)
    {
        JsonSchema schema = JsonSchema.FromType(type, SchemaSettings);
        return JsonSchemaDocument.FromJson(schema.ToJson());
    }
}
