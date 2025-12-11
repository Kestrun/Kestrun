using System.Collections;
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
        return value is null
            ? null
            : value switch
            {
                bool b => JsonValue.Create(b),
                string s => JsonValue.Create(s),
                sbyte or byte or short or ushort or int or uint or long or ulong => JsonValue.Create(Convert.ToInt64(value)),
                float or double or decimal => JsonValue.Create(Convert.ToDouble(value)),
                DateTime dt => JsonValue.Create(dt.ToString("o")),
                Guid g => JsonValue.Create(g.ToString()),
                IDictionary dict => ToJsonObject(dict),
                IEnumerable en when value is not string => ToJsonArray(en),
                _ => FromPocoOrString(value),
            };
    }

    private static JsonObject ToJsonObject(IDictionary dict)
    {
        var obj = new JsonObject();
        foreach (DictionaryEntry de in dict)
        {
            if (de.Key is null)
            {
                continue;
            }

            var k = de.Key.ToString() ?? string.Empty;
            obj[k] = FromObject(de.Value);
        }
        return obj;
    }

    private static JsonArray ToJsonArray(IEnumerable en)
    {
        var arr = new JsonArray();
        foreach (var item in en)
        {
            arr.Add(FromObject(item));
        }
        return arr;
    }

    private static JsonNode FromPocoOrString(object value)
    {
        var t = value.GetType();
        if (!t.IsPrimitive && t != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(t))
        {
            var props = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (props.Length > 0)
            {
                var obj = new JsonObject();
                foreach (var p in props)
                {
                    if (!p.CanRead)
                    {
                        continue;
                    }

                    var v = p.GetValue(value);
                    if (v is null)
                    {
                        continue;
                    }

                    obj[p.Name] = FromObject(v);
                }
                return obj;
            }
        }
        return JsonValue.Create(value?.ToString() ?? string.Empty)!;
    }
}
