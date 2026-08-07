using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Contracts;
using Autodesk.Mcp.Shared.Enums;
using Autodesk.Mcp.Shared.Errors;
using Autodesk.Mcp.Shared.Schemas;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>
/// The single, shared JSON configuration for the bridge protocol. Both the bridge and the protocol
/// layer MUST use these options so the wire format stays consistent. The cached <see cref="Options"/>
/// instance is immutable after construction and safe for concurrent reads.
/// </summary>
public static class SharedJson
{
    /// <summary>
    /// The shared, frozen serializer options: camelCase names, nulls omitted, tolerant reads
    /// (unknown properties skipped, numbers accepted as strings, unknown enums fall back to
    /// sentinel values, versions read from string or object).
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateDefault();

    /// <summary>Creates a fresh copy of the shared options (for example for source-generated contexts).</summary>
    public static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = false,
        };

        options.Converters.Add(new TolerantEnumConverter<ToolCategory>());
        options.Converters.Add(new TolerantEnumConverter<ToolPermission>());
        options.Converters.Add(new TolerantEnumConverter<ToolRisk>());
        options.Converters.Add(new TolerantEnumConverter<ErrorCode>());
        options.Converters.Add(new JsonRpcIdConverter());
        options.Converters.Add(new VersionInformationConverter());
        options.Converters.Add(new JsonSchemaDocumentConverter());
        return options;
    }
}
