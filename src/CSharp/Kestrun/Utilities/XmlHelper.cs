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
    /// <summary>
    /// Default maximum recursion depth for object-to-XML conversion.
    /// Chosen to balance performance and stack safety for typical object graphs.
    /// Adjust if deeper object graphs need to be serialized.
    /// </summary>
    public const int DefaultMaxDepth = 32;

    /// <summary>
    /// Maximum recursion depth for object-to-XML conversion. This value can be adjusted if deeper object graphs need to be serialized.
    /// </summary>
    public static int MaxDepth { get; set; } = DefaultMaxDepth;
    // Per-call cycle detection now passed explicitly (was ThreadStatic). Avoids potential thread reuse memory retention.
    // Rationale:
    //   * ThreadStatic HashSet could retain large object graphs across requests in thread pool threads causing memory bloat.
    //   * A per-call HashSet has a short lifetime and becomes eligible for GC immediately after serialization completes.
    //   * Passing the set by reference keeps allocation to a single HashSet per root ToXml call (lazy created on first reference type).
    //   * No synchronization needed: the set is confined to the call stack; recursive calls share it by reference.

    /// <summary>
    /// Converts an object to an <see cref="XElement"/> with the specified name, handling nulls, primitives, dictionaries, enumerables, and complex types.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert to XML.</param>
    /// <returns>An <see cref="XElement"/> representing the object.</returns>
    public static XElement ToXml(string name, object? value) => ToXmlInternal(SanitizeName(name), value, 0, visited: null);

    /// <summary>
    /// Internal recursive method to convert an object to XML, with depth tracking and cycle detection.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert to XML.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <returns>An <see cref="XElement"/> representing the object.</returns>
    /// <param name="visited">Per-call set of already visited reference objects for cycle detection.</param>
    private static XElement ToXmlInternal(string name, object? value, int depth, HashSet<object>? visited)
    {
        // Fast path & terminal cases extracted to helpers for reduced branching complexity.
        if (TryHandleTerminal(name, value, depth, ref visited, out var terminal))
        {
            return terminal;
        }

        // At this point value is non-null complex/reference or value-type object requiring reflection.
        var type = value!.GetType();
        var needsCycleTracking = !type.IsValueType;
        if (needsCycleTracking && !EnterCycle(value, ref visited, out var cycleElem))
        {
            return cycleElem!; // Cycle detected
        }

        try
        {
            return ObjectToXml(name, value, depth, visited);
        }
        finally
        {
            if (needsCycleTracking && visited is not null)
            {
                _ = visited.Remove(value);
            }
        }
    }

    /// <summary>
    /// Handles depth guard, null, enums, primitives, simple temporal types, dictionaries &amp; enumerables.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert to XML.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <param name="element">The resulting XML element if handled; otherwise, null.</param>
    /// <param name="visited">Per-call set used for cycle detection (reference types only).</param>
    /// <returns><c>true</c> if the value was handled; otherwise, <c>false</c>.</returns>
    private static bool TryHandleTerminal(string name, object? value, int depth, ref HashSet<object>? visited, out XElement element)
    {
        // Depth guard handled below.
        if (depth >= MaxDepth)
        {
            element = new XElement(name, new XAttribute("warning", "MaxDepthExceeded"));
            return true;
        }

        // Null
        if (value is null)
        {
            element = new XElement(name, new XAttribute(xsi + "nil", true));
            return true;
        }

        var type = value.GetType();

        // Enum
        if (type.IsEnum)
        {
            element = new XElement(name, value.ToString());
            return true;
        }

        // Primitive / simple
        if (IsSimple(value))
        {
            element = new XElement(name, value);
            return true;
        }

        // DateTimeOffset / TimeSpan (already covered by IsSimple for DateTimeOffset/TimeSpan? DateTimeOffset yes, TimeSpan yes) but keep explicit for clarity / format control
        if (value is DateTimeOffset dto)
        {
            element = new XElement(name, dto.ToString("O"));
            return true;
        }
        if (value is TimeSpan ts)
        {
            element = new XElement(name, ts.ToString());
            return true;
        }

        // IDictionary
        if (value is IDictionary dict)
        {
            element = DictionaryToXml(name, dict, depth, visited);
            return true;
        }

        // IEnumerable
        if (value is IEnumerable enumerable)
        {
            element = EnumerableToXml(name, enumerable, depth, visited);
            return true;
        }

        element = null!;
        return false;
    }

    /// <summary>
    /// Enters cycle tracking for the specified object. Returns false if a cycle is detected.
    /// </summary>
    /// <param name="value">The object to track.</param>
    /// <param name="cycleElement">The resulting XML element if a cycle is detected; otherwise, null.</param>
    /// <returns><c>true</c> if the object is successfully tracked; otherwise, <c>false</c>.</returns>
    /// <param name="visited">Per-call set of visited objects (created lazily).</param>
    private static bool EnterCycle(object value, ref HashSet<object>? visited, out XElement? cycleElement)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(value))
        {
            cycleElement = new XElement("Object", new XAttribute("warning", "CycleDetected"));
            return false;
        }
        cycleElement = null;
        return true;
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
    /// <param name="visited">Per-call set used for cycle detection.</param>
    private static XElement DictionaryToXml(string name, IDictionary dict, int depth, HashSet<object>? visited)
    {
        var elem = new XElement(name);
        foreach (DictionaryEntry entry in dict)
        {
            var key = SanitizeName(entry.Key?.ToString() ?? "Key");
            elem.Add(ToXmlInternal(key, entry.Value, depth + 1, visited));
        }
        return elem;
    }
    /// <summary>Converts an enumerable to an XML element; each item becomes &lt;Item/&gt;.</summary>
    /// <param name="name">Element name for the collection.</param>
    /// <param name="enumerable">Sequence to serialize.</param>
    /// <param name="depth">Current recursion depth (guarded).</param>
    /// <param name="visited">Per-call set used for cycle detection.</param>
    private static XElement EnumerableToXml(string name, IEnumerable enumerable, int depth, HashSet<object>? visited)
    {
        var elem = new XElement(name);
        foreach (var item in enumerable)
        {
            elem.Add(ToXmlInternal("Item", item, depth + 1, visited));
        }
        return elem;
    }

    /// <summary>Reflects an object's public instance properties into XML.</summary>
    /// <param name="name">Element name for the object.</param>
    /// <param name="value">Object instance to serialize.</param>
    /// <param name="depth">Current recursion depth (guarded).</param>
    /// <param name="visited">Per-call set used for cycle detection.</param>
    private static XElement ObjectToXml(string name, object value, int depth, HashSet<object>? visited)
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
            objElem.Add(ToXmlInternal(childName, propVal, depth + 1, visited));
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
