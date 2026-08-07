using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>
/// Serializes enums as their exact member names (for example <c>ModifyDrawing</c>) and reads them
/// back case-insensitively. Unknown string or numeric values do not throw: they fall back to the
/// member named <c>Unknown</c> when present, otherwise to the first declared member. This keeps
/// the wire tolerant when a newer peer sends values this build does not know yet.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static class Metadata
    {
        internal static readonly TEnum[] Values = Enum.GetValues<TEnum>();
        internal static readonly string[] Names = Array.ConvertAll(Values, static v => v.ToString());
        internal static readonly TEnum Fallback = FindFallback();

        private static TEnum FindFallback()
        {
            int unknown = Array.IndexOf(Names, "Unknown");
            if (unknown >= 0)
            {
                return Values[unknown];
            }

            return Values.Length > 0 ? Values[0] : default;
        }
    }

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Metadata.Fallback;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? text = reader.GetString();
            if (text is not null)
            {
                for (int i = 0; i < Metadata.Names.Length; i++)
                {
                    if (string.Equals(Metadata.Names[i], text, StringComparison.OrdinalIgnoreCase))
                    {
                        return Metadata.Values[i];
                    }
                }

                // A pure-numeric string (for example "3" under AllowReadingFromString) is
                // treated as the underlying numeric value, matching bare-number handling.
                if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long numeric))
                {
                    for (int i = 0; i < Metadata.Values.Length; i++)
                    {
                        if (Convert.ToInt64(Metadata.Values[i]) == numeric)
                        {
                            return Metadata.Values[i];
                        }
                    }
                }
            }

            return Metadata.Fallback;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out long number))
        {
            for (int i = 0; i < Metadata.Values.Length; i++)
            {
                if (Convert.ToInt64(Metadata.Values[i]) == number)
                {
                    return Metadata.Values[i];
                }
            }

            return Metadata.Fallback;
        }

        throw new JsonException($"Unexpected token '{reader.TokenType}' while reading enum '{typeof(TEnum).Name}'.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        for (int i = 0; i < Metadata.Values.Length; i++)
        {
            if (EqualityComparer<TEnum>.Default.Equals(Metadata.Values[i], value))
            {
                writer.WriteStringValue(Metadata.Names[i]);
                return;
            }
        }

        writer.WriteStringValue(value.ToString());
    }
}
