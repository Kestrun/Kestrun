// Portions derived from PowerShell-Yaml (https://github.com/cloudbase/powershell-yaml)
// Copyright (c) 2016–2024 Cloudbase Solutions Srl
// Licensed under the Apache License, Version 2.0 (Apache-2.0).
// http://www.apache.org/licenses/LICENSE-2.0
// Modifications Copyright (c) 2025 Kestrun Contributors

using System.Collections;
using System.Collections.Specialized;


namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Helpers to normalize PowerShell objects into plain .NET structures:
/// - OrderedDictionary for maps (preserving insertion order)
/// - List&lt;object?&gt; for sequences
/// - Unwraps PSObject wrappers
/// </summary>
public static class PsObjectConverter
{
    /// <summary>
    /// Convert a PSObject/.NET object into a "generic" form:
    /// OrderedDictionary for dictionaries, List&lt;object?&gt; for lists, and
    /// recursively converts nested values. Leaves scalars unchanged.
    /// </summary>
    public static object? ConvertPSObjectToGenericObject(object? data)
    {
        if (data is null)
        {
            return null;
        }

        // Unwrap PSObject if present (without requiring Microsoft.PowerShell.SDK at compile time)
        data = UnwrapPSObjectIfAny(data);
        if (data is null)
        {
            return null;
        }

        // Ordered dictionary → convert values in place
        if (data is OrderedDictionary od)
        {
            return ConvertOrderedHashtableToDictionary(od);
        }

        // Any IDictionary → new OrderedDictionary with recursively converted values
        if (data is IDictionary dict)
        {
            return ConvertHashtableToDictionary(dict);
        }

        // Arrays & lists → List<object?>
        if (data is IList list)
        {
            return ConvertListToGenericList(list);
        }

        // Scalar or unknown → leave as-is
        return data;
    }

    /// <summary>
    /// Convert an OrderedDictionary by recursively normalizing its values.
    /// Keys and order are preserved; the same instance is returned.
    /// </summary>
    /// <param name="data">The ordered hashtable to convert.</param>
    /// <returns>The same OrderedDictionary instance with converted values.</returns>
    public static OrderedDictionary ConvertOrderedHashtableToDictionary(OrderedDictionary data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Copy keys to avoid mutating during enumeration
        var keys = data.Keys.Cast<object>().ToList();
        foreach (var key in keys)
        {
            var value = data[key];
            data[key] = ConvertPSObjectToGenericObject(value);
        }
        return data;
    }

    /// <summary>
    /// Convert any IDictionary into an OrderedDictionary, preserving the enumeration order
    /// of the source dictionary and recursively normalizing values.
    /// NOTE: PowerShell hashtables typically enumerate in insertion order; plain .NET Hashtable
    /// does not guarantee it. We preserve whatever order the IDictionary provides.
    /// </summary>
    /// <param name="data">The hashtable to convert.</param>
    /// <returns>An OrderedDictionary containing the converted entries.</returns>
    public static OrderedDictionary ConvertHashtableToDictionary(IDictionary data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var ordered = new OrderedDictionary();
        foreach (DictionaryEntry entry in data)
        {
            var key = entry.Key;
            var value = ConvertPSObjectToGenericObject(entry.Value);
            ordered.Add(key!, value);
        }
        return ordered;
    }

    /// <summary>
    /// Convert any IList (including arrays) into a List&lt;object?&gt;,
    /// recursively normalizing each element.
    /// </summary>
    /// <param name="data">The list to convert.</param>
    /// <returns>A List&lt;object?&gt; containing the converted elements.</returns>
    public static List<object?> ConvertListToGenericList(IList data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var ret = new List<object?>(data.Count);
        for (var i = 0; i < data.Count; i++)
        {
            ret.Add(ConvertPSObjectToGenericObject(data[i]));
        }
        return ret;
    }

    /// <summary>
    /// Gently unwraps System.Management.Automation.PSObject if the SDK is present,
    /// without introducing a hard compile-time dependency.
    /// </summary>
    /// <param name="obj">The object to unwrap if it's a PSObject.</param>
    /// <returns>The unwrapped BaseObject if input was a PSObject; otherwise, the original object.</returns>
    private static object? UnwrapPSObjectIfAny(object obj)
    {
        var t = obj.GetType();
        if (t.FullName == "System.Management.Automation.PSObject")
        {
            // Use reflection to read PSObject.BaseObject
            var baseObjProp = t.GetProperty("BaseObject");
            return baseObjProp?.GetValue(obj);
        }
        return obj;
    }
}
