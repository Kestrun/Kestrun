using System.Text.Json;
namespace Kestrun.Utilities.Json;

/// <summary>
/// Helper class for JSON serialization and deserialization using System.Text.Json.
/// </summary>
public static class JsonSerializerHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // If you want enums as strings etc, you can add converters here
        // Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Deserializes the given JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T"> The type of the object to deserialize to. </typeparam>
    /// <param name="json"> The JSON string to deserialize. </param>
    /// <returns> The deserialized object of type T. </returns>
    public static T FromJson<T>(string json)
    {
        var result = JsonSerializer.Deserialize<T>(json, Options);
        return result is null ? throw new JsonException($"Deserialization of type '{typeof(T)}' from JSON failed.") : result;
    }

    /// <summary>
    /// Deserializes the given JSON string to an object of the specified type.
    /// </summary>
    /// <param name="json"> The JSON string to deserialize. </param>
    /// <param name="type"> The type to which the JSON string should be deserialized. </param>
    /// <returns> The deserialized object. </returns>
    public static object FromJson(string json, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var result = JsonSerializer.Deserialize(json, type, Options);
        return result is null ? throw new JsonException($"Deserialization of type '{type}' from JSON failed.") : result;
    }
}
