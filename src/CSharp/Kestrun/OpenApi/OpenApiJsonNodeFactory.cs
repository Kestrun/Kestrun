using System.Collections;
using System.Management.Automation;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;
/// <summary>
/// Helpers to create System.Text.Json.Nodes from .NET objects for OpenAPI representation.
/// </summary>
public static class OpenApiJsonNodeFactory
{
    /// <summary>
    /// Create a JsonNode from a .NET object.
    /// </summary>
    /// <param name="value">The .NET object to convert.</param>
    /// <returns>A JsonNode representation of the object.</returns>
    public static JsonNode? FromObject(object? value)
    {
        value = Unwrap(value);
        if (value is null) { return null; }

        // Handle various common types
        return value switch
        {
            // primitives
            bool b => JsonValue.Create(b),
            string s => JsonValue.Create(s),

            // integers (preserve range; avoid ulong->long overflow)
            sbyte sb => JsonValue.Create((long)sb),
            byte by => JsonValue.Create((long)by),
            short sh => JsonValue.Create((long)sh),
            ushort ush => JsonValue.Create((long)ush),
            int i => JsonValue.Create((long)i),
            uint ui => JsonValue.Create((ulong)ui <= long.MaxValue ? ui : (decimal)ui),
            long l => JsonValue.Create(l),
            ulong ul => ul <= long.MaxValue ? JsonValue.Create((long)ul) : JsonValue.Create((decimal)ul),

            // floating/decimal
            float f => JsonValue.Create((double)f),
            double d => JsonValue.Create(d),
            decimal m => JsonValue.Create(m),

            // common .NET types
            DateTime dt => JsonValue.Create(dt.ToString("o")),
            DateTimeOffset dto => JsonValue.Create(dto.ToString("o")),
            TimeSpan ts => JsonValue.Create(ts.ToString("c")),
            Guid g => JsonValue.Create(g.ToString()),
            Uri uri => JsonValue.Create(uri.ToString()),

            // enums -> string (usually nicer for OpenAPI-ish metadata)
            Enum e => JsonValue.Create(e.ToString()),

            // dictionaries / lists
            IDictionary dict => ToJsonObject(dict),
            IEnumerable en when value is not string => ToJsonArray(en),

            // fallback
            _ => FromPocoOrString(value),
        };
    }

    /// <summary>
    /// Unwraps common wrapper types to get the underlying value.
    /// </summary>
    /// <param name="value">The object to unwrap.</param>
    /// <returns>The unwrapped object, or the original if no unwrapping was needed.</returns>
    private static object? Unwrap(object? value)
    {
        if (value is null)
        {
            return null;
        }
        // PowerShell wraps lots of values in PSObject
        if (value is PSObject pso)
        {
            // If it's a PSCustomObject / has note properties, keep PSObject itself
            // so we can serialize its Properties cleanly.
            // Otherwise unwrap to BaseObject.
            return pso.BaseObject is not null && pso.BaseObject.GetType() != typeof(PSCustomObject) ?
                pso.BaseObject : pso;
        }

        return value;
    }

    /// <summary>
    /// Converts an IDictionary to a JsonObject.
    /// </summary>
    /// <param name="dict">The dictionary to convert.</param>
    /// <returns>A JsonObject representing the dictionary.</returns>
    private static JsonObject ToJsonObject(IDictionary dict)
    {
        var obj = new JsonObject();

        foreach (DictionaryEntry de in dict)
        {
            if (de.Key is null)
            {
                continue;
            }

            var k = de.Key.ToString();
            if (string.IsNullOrWhiteSpace(k))
            {
                continue;
            }

            obj[k] = FromObject(de.Value);
        }

        return obj;
    }

    /// <summary>
    /// Converts an IEnumerable to a JsonArray.
    /// </summary>
    /// <param name="en">The enumerable to convert.</param>
    /// <returns>A JsonArray representing the enumerable.</returns>
    private static JsonArray ToJsonArray(IEnumerable en)
    {
        var arr = new JsonArray();
        foreach (var item in en)
        {
            arr.Add(FromObject(item));
        }
        return arr;
    }

    /// <summary>
    /// Converts a POCO or other object to a JsonNode by reflecting its public properties.
    /// </summary>
    /// <param name="value">The object to convert.</param>
    /// <returns>A JsonNode representing the object.</returns>
    private static JsonNode FromPocoOrString(object value)
    {
        if (TryConvertPsObjectProperties(value, out var psObject))
        {
            return psObject;
        }

        var type = value.GetType();
        // Skip null property values to avoid serializing them in the OpenAPI document.
        // Avoid reflecting on common framework types
        if (ShouldFallbackToString(type))
        {
            return JsonValue.Create(value.ToString() ?? string.Empty);
        }

        if (TryConvertPublicProperties(value, type, out var poco))
        {
            return poco;
        }
        // Fallback to string representation
        return JsonValue.Create(value.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Attempts to serialize a PowerShell object using its dynamic properties.
    /// </summary>
    /// <param name="value">The input value to inspect.</param>
    /// <param name="node">A JsonObject containing serialized PowerShell properties when successful.</param>
    /// <returns>True when properties were found and serialized; otherwise false.</returns>
    private static bool TryConvertPsObjectProperties(object value, out JsonNode node)
    {
        node = null!;

        // If it's a PowerShell object with properties, serialize those rather than reflection on the proxy type.
        if (value is not PSObject pso)
        {
            return false;
        }

        var obj = new JsonObject();
        foreach (var prop in pso.Properties)
        {
            var v = prop.Value;
            if (v is null)
            {
                continue;
            }

            obj[prop.Name] = FromObject(v);
        }

        if (obj.Count == 0)
        {
            return false;
        }

        node = obj;
        return true;
    }

    /// <summary>
    /// Determines whether an object of the specified type should be represented as a string
    /// instead of reflecting public properties.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True when the type should be treated as a scalar/string; otherwise false.</returns>
    private static bool ShouldFallbackToString(Type type) =>
        type.IsPrimitive || type == typeof(string) || typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// Attempts to serialize a POCO by reflecting its public, readable, non-indexer instance properties.
    /// </summary>
    /// <param name="value">The object instance to read properties from.</param>
    /// <param name="type">The runtime type of <paramref name="value"/>.</param>
    /// <param name="node">A JsonObject when at least one property was serialized.</param>
    /// <returns>True when properties were found and serialized; otherwise false.</returns>
    private static bool TryConvertPublicProperties(object value, Type type, out JsonNode node)
    {
        node = null!;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length == 0)
        {
            return false;
        }

        var obj = new JsonObject();
        foreach (var p in props)
        {
            if (!IsReadableNonIndexer(p))
            {
                continue;
            }

            if (!TryGetPropertyValue(p, value, out var v) || v is null)
            {
                continue;
            }

            obj[p.Name] = FromObject(v);
        }

        if (obj.Count == 0)
        {
            return false;
        }

        node = obj;
        return true;
    }

    /// <summary>
    /// Checks whether a property is readable and not an indexer.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <returns>True when the property can be read and has no index parameters.</returns>
    private static bool IsReadableNonIndexer(PropertyInfo property) =>
        property.CanRead && property.GetIndexParameters().Length == 0;

    /// <summary>
    /// Attempts to read a property value and safely handles reflection exceptions.
    /// </summary>
    /// <param name="property">The property to read.</param>
    /// <param name="instance">The instance to read the value from.</param>
    /// <param name="value">The retrieved value, or null if not available.</param>
    /// <returns>True when the value was retrieved successfully; otherwise false.</returns>
    private static bool TryGetPropertyValue(PropertyInfo property, object instance, out object? value)
    {
        value = null;

        try
        {
            value = property.GetValue(instance);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
