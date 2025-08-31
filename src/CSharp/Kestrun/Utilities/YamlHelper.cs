using System.Collections;
using System.Management.Automation;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
namespace Kestrun.Utilities;

/// <summary>
/// Provides helper methods for serializing and deserializing YAML content, with special handling for PowerShell objects.
/// </summary>
public static class YamlHelper
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance).DisableAliases()
        .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Serializes any PowerShell object to YAML format.
    /// </summary>
    /// <param name="input">The PowerShell object to serialize. Can be null.</param>
    /// <returns>A string containing the YAML representation of the input object.</returns>
    public static string ToYaml(object? input)
    {
        var normalized = NormalizePSObject(input);
        return _serializer.Serialize(normalized);
    }

    /// <summary>
    /// Deserializes a YAML string into a PowerShell Hashtable.
    /// </summary>
    /// <param name="yaml">The YAML string to deserialize.</param>
    /// <returns>A Hashtable containing the deserialized YAML content.</returns>
    public static Hashtable ToHashtable(string yaml)
    {
        if (string.IsNullOrEmpty(yaml))
        {
            return [];
        }
        var obj = _deserializer.Deserialize<object>(yaml);
        return (Hashtable)ConvertToPSCompatible(obj);
    }

    /// <summary>
    /// Deserializes a YAML string into a PowerShell PSObject (PSCustomObject).
    /// </summary>
    /// <param name="yaml">The YAML string to deserialize.</param>
    /// <returns>A PSObject containing the deserialized YAML content.</returns>
    public static PSObject ToPSCustomObject(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var obj = _deserializer.Deserialize<object>(yaml);
        var hash = (Hashtable)ConvertToPSCompatible(obj);
        return ConvertToPSCustomObject(hash);
    }

    /// <summary>
    /// Normalizes a PowerShell object into a plain .NET structure that can be serialized to YAML.
    /// </summary>
    /// <param name="obj">The object to normalize. Can be null.</param>
    /// <returns>A normalized object suitable for YAML serialization, or null if the input is null.</returns>
    /// <remarks>
    /// This method handles various PowerShell-specific types and converts them into standard .NET types:
    /// - PSObjects are unwrapped to their base objects
    /// - Dictionaries are converted to Dictionary&lt;object, object?&gt;
    /// - Collections are converted to List&lt;object&gt;
    /// - Objects with properties are converted to Dictionary&lt;string, object?&gt;
    /// </remarks>
    private static object? NormalizePSObject(object? obj)
    {
        // Unwrap PSObject
        if (obj is PSObject psObj)
        {
            return NormalizePSObject(psObj.BaseObject);
        }

        // Dictionaries → Dictionary<object, object?>
        if (obj is IDictionary dict)
        {
            return NormalizeDictionary(dict);
        }

        // Enumerables (not string) → List<object?>
        if (obj is IEnumerable enumerable and not string)
        {
            return NormalizeEnumerable(enumerable);
        }

        // Null, primitives, and string → return as-is
        if (obj is null || obj.GetType().IsPrimitive || obj is string)
        {
            return obj;
        }

        // Objects with properties → Dictionary<string, object?>
        return NormalizeByProperties(obj);
    }

    private static Dictionary<object, object?> NormalizeDictionary(IDictionary dict)
    {
        var d = new Dictionary<object, object?>();
        foreach (var key in dict.Keys)
        {
            var value = dict[key];
            d[key] = NormalizePSObject(value);
        }
        return d;
    }

    private static List<object?> NormalizeEnumerable(IEnumerable enumerable)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(NormalizePSObject(item));
        }

        return list;
    }

    private static Dictionary<string, object?> NormalizeByProperties(object obj)
    {
        var result = new Dictionary<string, object?>();
        var props = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var prop in props)
        {
            // Skip indexers
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            try
            {
                var propValue = prop.GetValue(obj);
                result[prop.Name] = NormalizePSObject(propValue);
            }
            catch
            {
                result[prop.Name] = null;
            }
        }
        return result;
    }

    /// <summary>
    /// Converts a deserialized YAML object into PowerShell-compatible types recursively.
    /// </summary>
    /// <param name="obj">The object to convert.</param>
    /// <returns>A PowerShell-compatible object structure using Hashtable and ArrayList.</returns>
    /// <remarks>
    /// This method performs the following conversions:
    /// - Dictionaries are converted to PowerShell Hashtables
    /// - Lists are converted to ArrayLists
    /// - Null values are converted to empty strings
    /// </remarks>
    private static object ConvertToPSCompatible(object obj)
    {
        switch (obj)
        {
            case IDictionary dict:
                var ht = new Hashtable();
                foreach (DictionaryEntry entry in dict)
                {
                    ht[entry.Key] = entry.Value is not null ? ConvertToPSCompatible(entry.Value) : null;
                }

                return ht;

            case IList list:
                var array = new ArrayList();
                foreach (var item in list)
                {
                    _ = array.Add(ConvertToPSCompatible(item));
                }

                return array;

            default:
                return obj ?? string.Empty;
        }
    }

    /// <summary>
    /// Converts a Hashtable or ArrayList into a PowerShell PSObject (PSCustomObject) recursively.
    /// </summary>
    /// <param name="obj">The object to convert, typically a Hashtable or ArrayList.</param>
    /// <returns>A PSObject representing the input structure.</returns>
    /// <remarks>
    /// This method performs deep conversion of nested structures:
    /// - Hashtables are converted to PSObjects with NoteProperties
    /// - ArrayLists are converted to arrays of PSObjects
    /// - Other types are wrapped in PSObject using AsPSObject
    /// </remarks>
    private static PSObject ConvertToPSCustomObject(object obj)
    {
        if (obj is Hashtable ht)
        {
            var result = new PSObject();
            foreach (DictionaryEntry entry in ht)
            {
                var key = entry.Key.ToString();
                var value = entry.Value;

                if (value is Hashtable or ArrayList)
                {
                    value = ConvertToPSCustomObject(value);
                }

                if (key != null)
                {
                    AddMember(result, key, value ?? string.Empty);
                }
            }
            return result;
        }
        else if (obj is ArrayList list)
        {
            var resultList = new List<object>();
            foreach (var item in list)
            {
                resultList.Add(ConvertToPSCustomObject(item));
            }

            return new PSObject(resultList.ToArray());
        }

        return PSObject.AsPSObject(obj);
    }

    /// <summary>
    /// Adds a new note property to a PSObject.
    /// </summary>
    /// <param name="obj">The PSObject to add the property to.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    private static void AddMember(PSObject obj, string name, object value) => obj.Properties.Add(new PSNoteProperty(name, value));
}
