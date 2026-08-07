using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Mcp.Shared.Contracts;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>
/// Serializes <see cref="VersionInformation"/> as its compact string form (<c>1.2.3-beta.1+build</c>)
/// and reads it back from a string or, for tolerance, from an object form.
/// </summary>
public sealed class VersionInformationConverter : JsonConverter<VersionInformation>
{
    /// <inheritdoc />
    public override VersionInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? text = reader.GetString();
            if (text is not null && VersionInformation.TryParse(text, out VersionInformation version))
            {
                return version;
            }

            throw new JsonException($"'{text}' is not a valid semantic version.");
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int major = 0, minor = 0, patch = 0;
            string pre = string.Empty, build = string.Empty;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Malformed version object.");
                }

                string property = reader.GetString() ?? string.Empty;
                reader.Read();
                switch (property.ToLowerInvariant())
                {
                    case "major": major = ReadInt32(ref reader); break;
                    case "minor": minor = ReadInt32(ref reader); break;
                    case "patch": patch = ReadInt32(ref reader); break;
                    case "prerelease": pre = reader.GetString() ?? string.Empty; break;
                    case "buildmetadata": build = reader.GetString() ?? string.Empty; break;
                    default: reader.Skip(); break;
                }
            }

            return new VersionInformation(major, minor, patch, pre, build);
        }

        throw new JsonException($"Unexpected token '{reader.TokenType}' while reading a version.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, VersionInformation value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static int ReadInt32(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value))
        {
            return value;
        }

        if (reader.TokenType == JsonTokenType.String
            && int.TryParse(reader.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return 0;
    }
}
