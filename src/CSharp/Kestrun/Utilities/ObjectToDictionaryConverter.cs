using System.Collections;
using System.Reflection;

namespace Kestrun.Utilities;

/// <summary>
/// Utility for converting arbitrary .NET objects to a <see cref="Dictionary{TKey, TValue}"/> with string keys and values.
/// Handles dictionaries, enumerables, and objects with public properties.
/// </summary>
public static class ObjectToDictionaryConverter
{
    /// <summary>
    /// Converts an arbitrary object to a dictionary with string keys and string values.
    /// </summary>
    /// <param name="data">The object to convert. Can be a dictionary, enumerable, or any object with public properties.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> with string keys and string values.</returns>
    /// <remarks>
    /// Conversion behavior:
    /// - <see cref="IDictionary"/>: Converts keys and values to strings.
    /// - <see cref="IEnumerable"/> (excluding strings): Creates indexed keys like "item[0]", "item[1]", etc.
    /// - Other objects: Extracts public properties as key/value pairs.
    /// </remarks>
    public static Dictionary<string, string> ToDictionary(object? data)
    {
        return data switch
        {
            null => [],
            IDictionary dictionary => FromDictionaryToString(dictionary),
            IEnumerable enumerable when data is not string => FromEnumerableToString(enumerable),
            _ => FromObjectPropertiesToString(data)
        };
    }

    /// <summary>
    /// Converts an arbitrary object to a dictionary with string keys and object values (without stringification).
    /// </summary>
    /// <param name="data">The object to convert. Can be a dictionary, enumerable, or any object with public properties.</param>
    /// <returns>A <see cref="Dictionary{TKey, TValue}"/> with string keys and object values.</returns>
    /// <remarks>
    /// Conversion behavior mirrors <see cref="ToDictionary"/>, but preserves original types instead of converting to strings.
    /// </remarks>
    public static Dictionary<string, object?> ToDictionaryObject(object? data)
    {
        return data switch
        {
            null => [],
            IDictionary dictionary => FromDictionaryToObject(dictionary),
            IEnumerable enumerable when data is not string => FromEnumerableToObject(enumerable),
            _ => FromObjectPropertiesToObject(data)
        };
    }

    private static Dictionary<string, string> FromDictionaryToString(IDictionary source)
    {
        var dict = new Dictionary<string, string>();

        foreach (DictionaryEntry entry in source)
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            dict[key] = entry.Value?.ToString() ?? string.Empty;
        }

        return dict;
    }

    private static Dictionary<string, string> FromEnumerableToString(IEnumerable source)
    {
        var dict = new Dictionary<string, string>();
        var index = 0;

        foreach (var item in source)
        {
            dict[$"item[{index}]"] = item?.ToString() ?? string.Empty;
            index++;
        }

        return dict;
    }

    private static Dictionary<string, string> FromObjectPropertiesToString(object source)
    {
        var dict = new Dictionary<string, string>();
        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        foreach (var prop in properties)
        {
            dict[prop.Name] = GetPropertyStringValue(prop, source);
        }

        return dict;
    }

    private static string GetPropertyStringValue(PropertyInfo property, object source)
    {
        try
        {
            return property.GetValue(source)?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, object?> FromDictionaryToObject(IDictionary source)
    {
        var dict = new Dictionary<string, object?>();

        foreach (DictionaryEntry entry in source)
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            dict[key] = entry.Value;
        }

        return dict;
    }

    private static Dictionary<string, object?> FromEnumerableToObject(IEnumerable source)
    {
        var dict = new Dictionary<string, object?>();
        var index = 0;

        foreach (var item in source)
        {
            dict[$"item[{index}]"] = item;
            index++;
        }

        return dict;
    }

    private static Dictionary<string, object?> FromObjectPropertiesToObject(object source)
    {
        var dict = new Dictionary<string, object?>();
        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        foreach (var prop in properties)
        {
            dict[prop.Name] = GetPropertyObjectValue(prop, source);
        }

        return dict;
    }

    private static object? GetPropertyObjectValue(PropertyInfo property, object source)
    {
        try
        {
            return property.GetValue(source);
        }
        catch
        {
            return null;
        }
    }
}
