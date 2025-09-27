using System.Collections;
using System.Reflection;
using System.Xml.Linq;

namespace Kestrun.Utilities;

/// <summary>
/// Helpers for converting arbitrary objects into <see cref="XElement"/> instances.
/// </summary>
public static class XmlHelper
{
    private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private const int MaxDepth = 32;

    // Used for cycle detection
    [ThreadStatic]
    private static HashSet<object>? _visited;

    /// <summary>
    /// Converts an object to an <see cref="XElement"/> with the specified name, handling nulls, primitives, dictionaries, enumerables, and complex types.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert to XML.</param>
    /// <returns>An <see cref="XElement"/> representing the object.</returns>
    public static XElement ToXml(string name, object? value) => ToXmlInternal(SanitizeName(name), value, 0);

    private static XElement ToXmlInternal(string name, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            return new XElement(name, new XAttribute("warning", "MaxDepthExceeded"));
        }

        // null  → <name xsi:nil="true"/>
        if (value is null)
        {
            return new XElement(name, new XAttribute(xsi + "nil", true));
        }

        // Treat enums as their string name
        var type = value.GetType();
        if (type.IsEnum)
        {
            return new XElement(name, value.ToString());
        }

        // Primitive-like (extended) types
        if (IsSimple(value))
        {
            return new XElement(name, value);
        }

        // DateTimeOffset / TimeSpan explicit handling
        if (value is DateTimeOffset dto)
        {
            return new XElement(name, dto.ToString("O"));
        }
        if (value is TimeSpan ts)
        {
            return new XElement(name, ts.ToString());
        }

        // IDictionary (generic or non-generic)
        if (value is IDictionary dict)
        {
            return DictionaryToXml(name, dict, depth);
        }

        // IEnumerable (lists, arrays, StringValues, etc.)
        if (value is IEnumerable enumerable)
        {
            return EnumerableToXml(name, enumerable, depth);
        }

        // Cycle detection for reference types
        if (!type.IsValueType)
        {
            _visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!_visited.Add(value))
            {
                return new XElement(name, new XAttribute("warning", "CycleDetected"));
            }
        }

        try
        {
            var result = ObjectToXml(name, value, depth);
            return result;
        }
        finally
        {
            if (!type.IsValueType && _visited is not null)
            {
                _ = _visited.Remove(value);
                if (_visited.Count == 0)
                {
                    _visited = null; // reset for thread reuse
                }
            }
        }
    }

    /// <summary>
    /// Determines whether the specified object is a simple type (primitive, string, DateTime, Guid, or decimal).
    /// </summary>
    /// <param name="value">The object to check.</param>
    /// <returns><c>true</c> if the object is a simple type; otherwise, <c>false</c>.</returns>
    private static bool IsSimple(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive
            || value is string
            || value is DateTime or DateTimeOffset
            || value is Guid
            || value is decimal
            || value is TimeSpan;
    }

    /// <summary>Converts a dictionary to an XML element (recursive).</summary>
    /// <param name="name">Element name for the dictionary.</param>
    /// <param name="dict">Dictionary to serialize.</param>
    /// <param name="depth">Current recursion depth (guarded).</param>
    private static XElement DictionaryToXml(string name, IDictionary dict, int depth)
    {
        var elem = new XElement(name);
        foreach (DictionaryEntry entry in dict)
        {
            var key = SanitizeName(entry.Key?.ToString() ?? "Key");
            elem.Add(ToXmlInternal(key, entry.Value, depth + 1));
        }
        return elem;
    }
    /// <summary>Converts an enumerable to an XML element; each item becomes &lt;Item/&gt;.</summary>
    /// <param name="name">Element name for the collection.</param>
    /// <param name="enumerable">Sequence to serialize.</param>
    /// <param name="depth">Current recursion depth (guarded).</param>
    private static XElement EnumerableToXml(string name, IEnumerable enumerable, int depth)
    {
        var elem = new XElement(name);
        foreach (var item in enumerable)
        {
            elem.Add(ToXmlInternal("Item", item, depth + 1));
        }
        return elem;
    }

    /// <summary>Reflects an object's public instance properties into XML.</summary>
    /// <param name="name">Element name for the object.</param>
    /// <param name="value">Object instance to serialize.</param>
    /// <param name="depth">Current recursion depth (guarded).</param>
    private static XElement ObjectToXml(string name, object value, int depth)
    {
        var objElem = new XElement(name);
        var type = value.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue; // skip indexers
            }
            object? propVal;
            try
            {
                propVal = prop.GetValue(value);
            }
            catch
            {
                continue; // skip unreadable props
            }
            var childName = SanitizeName(prop.Name);
            objElem.Add(ToXmlInternal(childName, propVal, depth + 1));
        }
        return objElem;
    }

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Element";
        }
        // XML element names must start with letter or underscore; replace invalid chars with '_'
        var sb = new System.Text.StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            var valid = i == 0
                ? (char.IsLetter(ch) || ch == '_')
                : (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.');
            _ = sb.Append(valid ? ch : '_');
        }
        return sb.ToString();
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Converts an <see cref="XElement"/> into a <see cref="Hashtable"/>.
    /// Nested elements become nested Hashtables; repeated elements become lists.
    /// Attributes are stored as keys prefixed with "@", xsi:nil="true" becomes <c>null</c>.
    /// </summary>
    /// <param name="element">The XML element to convert.</param>
    /// <returns>A Hashtable representation of the XML element.</returns>
    public static Hashtable ToHashtable(XElement element)
    {
        var table = new Hashtable();

        // Handle attributes (as @AttributeName)
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.NamespaceName == xsi.NamespaceName && attr.Name.LocalName == "nil" && attr.Value == "true")
            {
                return new Hashtable { [element.Name.LocalName] = null! };
            }
            table["@" + attr.Name.LocalName] = attr.Value;
        }

        // If element has no children → treat as value
        if (!element.HasElements)
        {
            if (!string.IsNullOrWhiteSpace(element.Value))
            {
                table[element.Name.LocalName] = element.Value;
            }
            return table;
        }

        // Otherwise recurse into children
        var childMap = new Hashtable();
        foreach (var child in element.Elements())
        {
            var childKey = child.Name.LocalName;
            var childValue = ToHashtable(child);

            if (childMap.ContainsKey(childKey))
            {
                // Already exists → convert to List (allowing null values)
                if (childMap[childKey] is List<object?> list)
                {
                    list.Add(childValue[childKey]);
                }
                else
                {
                    childMap[childKey] = new List<object?>
                    {
                        childMap[childKey],
                        childValue[childKey]
                    };
                }
            }
            else
            {
                childMap[childKey] = childValue[childKey];
            }
        }

        table[element.Name.LocalName] = childMap;
        return table;
    }
}
