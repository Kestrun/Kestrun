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
        // If it's a PowerShell object with properties, serialize those rather than reflection on the proxy type
        if (value is PSObject pso)
        {
            var obj = new JsonObject();
            foreach (var prop in pso.Properties)
            {
                // skip nulls; optional: skip script methods etc.
                var v = prop.Value;
                if (v is null)
                {
                    continue;
                }

                obj[prop.Name] = FromObject(v);
            }
            // If it had no properties, fall back
            if (obj.Count > 0)
            {
                return obj;
            }
        }

        var t = value.GetType();

        // Avoid reflecting on common framework types
        if (t.IsPrimitive || t == typeof(string) || typeof(IEnumerable).IsAssignableFrom(t))
        {
            return JsonValue.Create(value.ToString() ?? string.Empty);
        }

        // Try POCO public instance readable props
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length > 0)
        {
            var obj = new JsonObject();

            foreach (var p in props)
            {
                if (!p.CanRead)
                {
                    continue;
                }

                // avoid indexers
                if (p.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object? v;
                try
                {
                    v = p.GetValue(value);
                }
                catch
                {
                    continue;
                }

                if (v is null)
                {
                    continue;
                }

                obj[p.Name] = FromObject(v);
            }

            if (obj.Count > 0)
            {
                return obj;
            }
        }
        return JsonValue.Create(value.ToString() ?? string.Empty);
    }
}
