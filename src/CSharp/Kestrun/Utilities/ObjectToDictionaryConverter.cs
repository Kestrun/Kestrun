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
        var dict = new Dictionary<string, string>();

        if (data is null)
        {
            return dict;
        }

        // Handle IDictionary
        if (data is IDictionary idict)
        {
            foreach (DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    dict[key] = entry.Value?.ToString() ?? string.Empty;
                }
            }
            return dict;
        }

        // Handle IEnumerable (but not string)
        if (data is IEnumerable and not string)
        {
            var enumerable = (IEnumerable)data;
            var index = 0;
            foreach (var item in enumerable)
            {
                dict[$"item[{index}]"] = item?.ToString() ?? string.Empty;
                index++;
            }
            return dict;
        }

        // Handle arbitrary objects: extract public properties
        var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(data);
                dict[prop.Name] = value?.ToString() ?? string.Empty;
            }
            catch
            {
                // Skip properties that throw on access
                dict[prop.Name] = string.Empty;
            }
        }

        return dict;
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
        var dict = new Dictionary<string, object?>();

        if (data is null)
        {
            return dict;
        }

        // Handle IDictionary
        if (data is IDictionary idict)
        {
            foreach (DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    dict[key] = entry.Value;
                }
            }
            return dict;
        }

        // Handle IEnumerable (but not string)
        if (data is IEnumerable and not string)
        {
            var enumerable = (IEnumerable)data;
            var index = 0;
            foreach (var item in enumerable)
            {
                dict[$"item[{index}]"] = item;
                index++;
            }
            return dict;
        }

        // Handle arbitrary objects: extract public properties
        var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(data);
                dict[prop.Name] = value;
            }
            catch
            {
                // Skip properties that throw on access
                dict[prop.Name] = null;
            }
        }

        return dict;
    }
}
