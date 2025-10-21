using System.Collections;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;

public static class OpenApiJsonNodeFactory
{
    public static JsonNode? FromObject(object? value)
    {
        if (value is null) return null;

        switch (value)
        {
            case bool b:
                return JsonValue.Create(b);
            case string s:
                return JsonValue.Create(s);
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                return JsonValue.Create(Convert.ToInt64(value));
            case float or double or decimal:
                return JsonValue.Create(Convert.ToDouble(value));
            case DateTime dt:
                return JsonValue.Create(dt.ToString("o"));
            case Guid g:
                return JsonValue.Create(g.ToString());
            case IDictionary dict:
                return ToJsonObject(dict);
            case IEnumerable en when value is not string:
                return ToJsonArray(en);
            default:
                return FromPocoOrString(value);
        }
    }

    private static JsonObject ToJsonObject(IDictionary dict)
    {
        var obj = new JsonObject();
        foreach (DictionaryEntry de in dict)
        {
            if (de.Key is null) continue;
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
                    if (!p.CanRead) continue;
                    var v = p.GetValue(value);
                    if (v is null) continue;
                    obj[p.Name] = FromObject(v);
                }
                return obj;
            }
        }
        return JsonValue.Create(value?.ToString() ?? string.Empty)!;
    }
}
