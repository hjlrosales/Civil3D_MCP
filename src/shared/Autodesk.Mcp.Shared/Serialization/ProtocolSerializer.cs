using System.Text.Json;

namespace Autodesk.Mcp.Shared.Serialization;

/// <summary>
/// Stateless facade over <see cref="SharedJson.Options"/> with convenience serialization helpers
/// used by the SDK and the bridge.
/// </summary>
public static class ProtocolSerializer
{
    /// <summary>Serializes a value to its wire JSON string.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SharedJson.Options);

    /// <summary>Deserializes a wire JSON string into a value.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The wire JSON.</param>
    /// <exception cref="JsonException">When the payload does not match the target type.</exception>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, SharedJson.Options);

    /// <summary>Attempts a deserialization without throwing.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The wire JSON.</param>
    /// <param name="value">The deserialized value when successful, otherwise default.</param>
    /// <returns>True when the payload deserialized successfully.</returns>
    public static bool TryDeserialize<T>(string json, out T? value)
    {
        try
        {
            value = Deserialize<T>(json);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }
}
