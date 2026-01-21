using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.OpenApi;
using OpenApiXmlModel = Microsoft.OpenApi.OpenApiXml;
using Serilog;

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

        // Check if the type has OpenAPI XML metadata.
        // Prefer a static XmlMetadata hashtable, otherwise build it from OpenApiXmlAttribute annotations.
        var xmlMetadata = GetOpenApiXmlMetadataForType(type);

        var needsCycleTracking = !type.IsValueType;
        if (needsCycleTracking && !EnterCycle(value, ref visited, out var cycleElem))
        {
            return cycleElem!; // Cycle detected
        }

        try
        {
            return ObjectToXml(name, value, depth, visited, xmlMetadata);
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
    /// <param name="xmlMetadata">Optional OpenAPI XML metadata hashtable (typically from a static XmlMetadata property).</param>
    private static XElement ObjectToXml(string name, object value, int depth, HashSet<object>? visited, Hashtable? xmlMetadata)
    {
        var elementName = ResolveObjectElementName(name, xmlMetadata);
        var objElem = new XElement(elementName);
        var type = value.GetType();

        var propertyMetadata = BuildPropertyMetadataLookup(xmlMetadata);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue; // skip indexers
            }
            if (!TryGetPropertyValue(prop, value, out var propVal))
            {
                continue; // skip unreadable props
            }

            // Check if property has OpenAPI XML metadata
            var propName = prop.Name;
            var childName = SanitizeName(propName);

            if (propertyMetadata != null && propertyMetadata.TryGetValue(propName, out var propXml))
            {
                AddPropertyWithMetadata(objElem, propName, propVal, propXml, depth, visited);
                continue;
            }

            objElem.Add(ToXmlInternal(childName, propVal, depth + 1, visited));
        }
        return objElem;
    }

    /// <summary>
    /// Resolves the element name used when serializing an object instance.
    /// </summary>
    /// <param name="defaultName">Default element name (typically the requested root name).</param>
    /// <param name="xmlMetadata">Optional OpenAPI XML metadata hashtable.</param>
    /// <returns>The resolved element name.</returns>
    private static string ResolveObjectElementName(string defaultName, Hashtable? xmlMetadata)
    {
        // Use class-level XML name if available.
        if (xmlMetadata?["ClassXml"] is Hashtable classXmlHash && classXmlHash["Name"] is string className)
        {
            return className;
        }

        if (xmlMetadata?["ClassName"] is string fallbackClassName && !string.IsNullOrWhiteSpace(fallbackClassName))
        {
            // When responses are written with the default root ("Response"), prefer the model's class name.
            // This aligns runtime XML output with OpenAPI schema component names.
            return fallbackClassName;
        }

        return defaultName;
    }

    /// <summary>
    /// Builds a property metadata lookup from a metadata hashtable.
    /// </summary>
    /// <param name="xmlMetadata">Optional OpenAPI XML metadata hashtable.</param>
    /// <returns>
    /// A dictionary mapping CLR property names to per-property metadata, or <c>null</c> when no property metadata is available.
    /// </returns>
    private static Dictionary<string, Hashtable>? BuildPropertyMetadataLookup(Hashtable? xmlMetadata)
    {
        if (xmlMetadata?["Properties"] is not Hashtable propsHash)
        {
            return null;
        }

        var propertyMetadata = new Dictionary<string, Hashtable>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in propsHash)
        {
            if (entry.Key is string propName && entry.Value is Hashtable propMeta)
            {
                propertyMetadata[propName] = propMeta;
            }
        }

        return propertyMetadata;
    }

    /// <summary>
    /// Attempts to read a property value via reflection.
    /// </summary>
    /// <param name="prop">Property info.</param>
    /// <param name="instance">Object instance.</param>
    /// <param name="value">Property value when readable.</param>
    /// <returns><c>true</c> when the property value was read; otherwise <c>false</c>.</returns>
    private static bool TryGetPropertyValue(PropertyInfo prop, object instance, out object? value)
    {
        try
        {
            value = prop.GetValue(instance);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Adds a property to an object element using OpenAPI XML metadata rules.
    /// </summary>
    /// <param name="parent">Parent element representing the object.</param>
    /// <param name="propName">CLR property name.</param>
    /// <param name="propVal">Property value.</param>
    /// <param name="propXml">OpenAPI XML metadata hashtable for the property.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <param name="visited">Per-call set used for cycle detection.</param>
    private static void AddPropertyWithMetadata(XElement parent, string propName, object? propVal, Hashtable propXml, int depth, HashSet<object>? visited)
    {
        var childName = SanitizeName(propName);

        // Use custom element name if specified.
        if (propXml["Name"] is string customName)
        {
            childName = customName;
        }

        // Check if this property should be an XML attribute.
        if (propXml["Attribute"] is bool isAttribute && isAttribute && propVal != null)
        {
            parent.Add(new XAttribute(childName, propVal));
            return;
        }

        // Special handling for array/list properties when XML metadata is present.
        // - If Wrapped=true, add a wrapper element and put items under it.
        // - If Wrapped=false, emit repeated sibling elements for each item.
        if (propVal is IEnumerable enumerable and not string)
        {
            var wrapped = propXml["Wrapped"] is bool w && w;
            var nsUri = propXml["Namespace"] as string;
            var prefix = propXml["Prefix"] as string;
            AppendEnumerableProperty(parent, childName, enumerable, wrapped, nsUri, prefix, depth + 1, visited);
            return;
        }

        // Apply namespace/prefix to the element representing this property (non-attribute, non-collection).
        var nsUriForElement = propXml["Namespace"] as string;
        var prefixForElement = propXml["Prefix"] as string;
        var childElem = ToXmlInternal(childName, propVal, depth + 1, visited);
        ApplyNamespaceAndPrefix(childElem, nsUriForElement, prefixForElement);
        parent.Add(childElem);
    }

    /// <summary>
    /// Appends an enumerable property to an existing element, honoring OpenAPI XML metadata options.
    /// </summary>
    /// <param name="parent">Parent element to append to.</param>
    /// <param name="itemName">Element name to use for items (and wrapper when <paramref name="wrapped"/> is true).</param>
    /// <param name="enumerable">Enumerable to serialize.</param>
    /// <param name="wrapped">If true, wrap items in a container element.</param>
    /// <param name="nsUri">Optional namespace URI for the element(s).</param>
    /// <param name="prefix">Optional prefix to declare for <paramref name="nsUri"/>.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <param name="visited">Per-call set used for cycle detection.</param>
    private static void AppendEnumerableProperty(XElement parent, string itemName, IEnumerable enumerable, bool wrapped, string? nsUri, string? prefix, int depth, HashSet<object>? visited)
    {
        if (wrapped)
        {
            var wrapper = new XElement(itemName);
            ApplyNamespaceAndPrefix(wrapper, nsUri, prefix);
            foreach (var item in enumerable)
            {
                var itemElem = ToXmlInternal(itemName, item, depth + 1, visited);
                ApplyNamespaceAndPrefix(itemElem, nsUri, prefix);
                wrapper.Add(itemElem);
            }
            parent.Add(wrapper);
            return;
        }

        foreach (var item in enumerable)
        {
            var itemElem = ToXmlInternal(itemName, item, depth + 1, visited);
            ApplyNamespaceAndPrefix(itemElem, nsUri, prefix);
            parent.Add(itemElem);
        }
    }

    /// <summary>
    /// Applies namespace and prefix settings to an element, ensuring the requested prefix is declared.
    /// </summary>
    /// <param name="element">The element to update.</param>
    /// <param name="nsUri">Namespace URI to apply.</param>
    /// <param name="prefix">Prefix to declare for the namespace.</param>
    private static void ApplyNamespaceAndPrefix(XElement element, string? nsUri, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(nsUri))
        {
            return;
        }

        var ns = XNamespace.Get(nsUri);
        element.Name = ns + element.Name.LocalName;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        // Ensure xmlns:prefix="nsUri" is present so the serializer can use the desired prefix.
        // Avoid adding duplicates if the declaration already exists.
        var xmlnsName = XNamespace.Xmlns + prefix;
        var existing = element.Attribute(xmlnsName);
        if (existing == null)
        {
            element.Add(new XAttribute(xmlnsName, nsUri));
        }
    }

    /// <summary>
    /// Attempts to retrieve OpenAPI XML metadata from a type.
    /// </summary>
    /// <param name="type">The type to check for metadata.</param>
    /// <returns>A Hashtable containing XML metadata, or null if none is available.</returns>
    private static Hashtable? TryGetXmlMetadata(Type type)
    {
        try
        {
            // Preferred: a static hashtable property/field (safe to read via reflection).
            // PowerShell class *methods* require an engine context on the current thread and may throw when invoked
            // from C# (even if the type was defined in a runspace).
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

            foreach (var value in EnumerateStaticMemberValues(type, flags, onlyNamedXmlMetadata: true))
            {
                var byName = AsMetadataHashtable(value);
                if (byName is not null)
                {
                    return byName;
                }
            }

            // Fallback: scan static members and return the first value that looks like XML metadata.
            foreach (var value in EnumerateStaticMemberValues(type, flags, onlyNamedXmlMetadata: false))
            {
                var candidate = AsMetadataHashtable(value);
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error retrieving XML metadata for type {TypeName}", type.FullName);
        }
        return null;
    }

    /// <summary>
    /// Enumerates values of static properties and fields on a type.
    /// </summary>
    /// <param name="type">Type to reflect.</param>
    /// <param name="flags">Binding flags used for reflection.</param>
    /// <param name="onlyNamedXmlMetadata">
    /// When <c>true</c>, only members named <c>XmlMetadata</c> (ignoring case and an optional leading <c>$</c>) are returned.
    /// </param>
    /// <returns>An enumerable of static member values (may include nulls).</returns>
    private static IEnumerable<object?> EnumerateStaticMemberValues(Type type, BindingFlags flags, bool onlyNamedXmlMetadata)
    {
        foreach (var prop in type.GetProperties(flags))
        {
            if (onlyNamedXmlMetadata && !NameMatchesXmlMetadata(prop.Name))
            {
                continue;
            }

            if (prop.GetIndexParameters().Length != 0)
            {
                continue;
            }

            yield return prop.GetValue(null);
        }

        foreach (var field in type.GetFields(flags))
        {
            if (onlyNamedXmlMetadata && !NameMatchesXmlMetadata(field.Name))
            {
                continue;
            }

            yield return field.GetValue(null);
        }
    }

    /// <summary>
    /// Determines whether a hashtable resembles the expected XML metadata shape.
    /// </summary>
    /// <param name="ht">Hashtable to inspect.</param>
    /// <returns><c>true</c> if the hashtable has the expected keys; otherwise <c>false</c>.</returns>
    private static bool LooksLikeXmlMetadata(Hashtable ht)
    {
        return ht.Count > 0
            && (ht.ContainsKey("ClassXml") || ht.ContainsKey("ClassName"))
            && ht.ContainsKey("Properties");
    }

    /// <summary>
    /// Attempts to normalize an object into an XML metadata hashtable.
    /// </summary>
    /// <param name="value">Candidate value (hashtable or dictionary).</param>
    /// <returns>A metadata hashtable when the candidate matches; otherwise <c>null</c>.</returns>
    private static Hashtable? AsMetadataHashtable(object? value)
    {
        if (value is Hashtable ht)
        {
            return LooksLikeXmlMetadata(ht) ? ht : null;
        }

        if (value is IDictionary dict)
        {
            var copied = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is string k)
                {
                    copied[k] = entry.Value;
                }
            }
            return LooksLikeXmlMetadata(copied) ? copied : null;
        }

        return null;
    }

    /// <summary>
    /// Checks whether a reflected member name corresponds to an <c>XmlMetadata</c> property/field.
    /// </summary>
    /// <param name="name">Member name.</param>
    /// <returns><c>true</c> when the name matches; otherwise <c>false</c>.</returns>
    private static bool NameMatchesXmlMetadata(string name)
    {
        var normalized = name.TrimStart('$');
        return string.Equals(normalized, "XmlMetadata", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets OpenAPI XML metadata for a CLR type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>
    /// A hashtable containing XML metadata (ClassName/ClassXml/Properties), or <c>null</c> when the type has no XML annotations.
    /// </returns>
    internal static Hashtable? GetOpenApiXmlMetadataForType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // First, try the static hashtable member (used by generated classes).
        var fromStatic = TryGetXmlMetadata(type);
        if (fromStatic is not null)
        {
            return fromStatic;
        }

        // Next, build metadata from attributes on the type (used by user-defined PowerShell classes).
        return BuildXmlMetadataFromAttributes(type);
    }

    /// <summary>
    /// Builds an XmlMetadata hashtable from <see cref="OpenApiXmlAttribute"/> annotations on a type and its members.
    /// This supports user-defined PowerShell classes that are not rewritten by the exporter.
    /// </summary>
    /// <param name="type">The annotated type.</param>
    /// <returns>An XmlMetadata hashtable, or <c>null</c> when the type has no XML annotations.</returns>
    private static Hashtable? BuildXmlMetadataFromAttributes(Type type)
    {
        var classXmlAttr = FindOpenApiXmlAttribute(type);
        var propsHash = BuildOpenApiXmlMemberMetadata(type);

        if (classXmlAttr is null && propsHash.Count == 0)
        {
            return null;
        }

        var meta = new Hashtable(StringComparer.OrdinalIgnoreCase)
        {
            ["ClassName"] = type.Name,
            ["Properties"] = propsHash,
        };

        if (classXmlAttr is not null)
        {
            var classXml = BuildOpenApiXmlClassMetadata(classXmlAttr);
            if (classXml.Count > 0)
            {
                meta["ClassXml"] = classXml;
            }
        }

        return meta;
    }

    /// <summary>
    /// Finds an <c>OpenApiXmlAttribute</c>-like attribute on the provided member or type.
    /// </summary>
    /// <param name="provider">The attribute provider to inspect.</param>
    /// <returns>The attribute instance when present; otherwise <c>null</c>.</returns>
    private static object? FindOpenApiXmlAttribute(ICustomAttributeProvider provider)
    {
        return provider
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a?.GetType().Name == "OpenApiXmlAttribute");
    }

    /// <summary>
    /// Reads a string-valued property from an attribute instance by reflection.
    /// </summary>
    /// <param name="attr">Attribute instance.</param>
    /// <param name="propName">Property name to read.</param>
    /// <returns>The string value when present; otherwise <c>null</c>.</returns>
    private static string? GetStringProperty(object attr, string propName)
        => attr.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(attr) as string;

    /// <summary>
    /// Reads a bool-valued property from an attribute instance by reflection.
    /// </summary>
    /// <param name="attr">Attribute instance.</param>
    /// <param name="propName">Property name to read.</param>
    /// <returns><c>true</c> when the property exists and evaluates to true; otherwise <c>false</c>.</returns>
    private static bool GetBoolProperty(object attr, string propName)
    {
        var value = attr.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(attr);
        return value is bool b && b;
    }

    /// <summary>
    /// Builds a metadata entry hashtable from an <c>OpenApiXmlAttribute</c>-like attribute instance.
    /// </summary>
    /// <param name="xmlAttr">Attribute instance.</param>
    /// <returns>A hashtable containing any provided XML metadata fields.</returns>
    private static Hashtable BuildEntryFromAttribute(object xmlAttr)
    {
        var entry = new Hashtable(StringComparer.OrdinalIgnoreCase);

        var name = GetStringProperty(xmlAttr, "Name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            entry["Name"] = name;
        }

        var ns = GetStringProperty(xmlAttr, "Namespace");
        if (!string.IsNullOrWhiteSpace(ns))
        {
            entry["Namespace"] = ns;
        }

        var prefix = GetStringProperty(xmlAttr, "Prefix");
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            entry["Prefix"] = prefix;
        }

        if (GetBoolProperty(xmlAttr, "Attribute"))
        {
            entry["Attribute"] = true;
        }

        if (GetBoolProperty(xmlAttr, "Wrapped"))
        {
            entry["Wrapped"] = true;
        }

        return entry;
    }

    /// <summary>
    /// Builds a property/field metadata hashtable by scanning public instance members for XML attributes.
    /// </summary>
    /// <param name="type">Type to scan.</param>
    /// <returns>A hashtable mapping member names to per-member metadata entries.</returns>
    private static Hashtable BuildOpenApiXmlMemberMetadata(Type type)
    {
        var propsHash = new Hashtable(StringComparer.OrdinalIgnoreCase);
        PopulateMemberMetadata(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), propsHash);
        PopulateMemberMetadata(type.GetFields(BindingFlags.Public | BindingFlags.Instance), propsHash);
        return propsHash;
    }

    /// <summary>
    /// Populates a metadata hashtable by scanning members for XML attribute annotations.
    /// </summary>
    /// <param name="members">Members to scan.</param>
    /// <param name="propsHash">Target metadata hashtable to populate.</param>
    private static void PopulateMemberMetadata(IEnumerable<MemberInfo> members, Hashtable propsHash)
    {
        foreach (var member in members)
        {
            var xmlAttr = FindOpenApiXmlAttribute(member);
            if (xmlAttr is null)
            {
                continue;
            }

            var entry = BuildEntryFromAttribute(xmlAttr);
            if (entry.Count > 0)
            {
                propsHash[member.Name] = entry;
            }
        }
    }

    /// <summary>
    /// Builds class-level XML metadata from an <c>OpenApiXmlAttribute</c>-like attribute instance.
    /// </summary>
    /// <param name="classXmlAttr">Class-level XML attribute instance.</param>
    /// <returns>A hashtable with class-level XML metadata.</returns>
    private static Hashtable BuildOpenApiXmlClassMetadata(object classXmlAttr)
    {
        var classXml = new Hashtable(StringComparer.OrdinalIgnoreCase);

        var classEntry = BuildEntryFromAttribute(classXmlAttr);
        foreach (DictionaryEntry entry in classEntry)
        {
            classXml[entry.Key] = entry.Value;
        }

        // Class-level metadata should not include member-only flags.
        classXml.Remove("Attribute");
        classXml.Remove("Wrapped");

        return classXml;
    }

    /// <summary>
    /// Sanitizes a raw string to be a valid XML element name by replacing invalid characters with underscores.
    /// </summary>
    /// <param name="raw">The raw string to sanitize.</param>
    /// <returns>A sanitized XML element name.</returns>
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
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Converts an <see cref="XElement"/> into a <see cref="Hashtable"/>, optionally using OpenAPI XML metadata hashtable (from PowerShell class metadata) to guide the conversion.
    /// Nested elements become nested Hashtables; repeated elements become lists.
    /// Attributes are stored as keys prefixed with "@" unless guided by OpenAPI metadata, xsi:nil="true" becomes <c>null</c>.
    /// </summary>
    /// <param name="element">The XML element to convert.</param>
    /// <param name="xmlMetadata">Optional OpenAPI XML metadata hashtable (as returned by GetOpenApiXmlMetadata()). Should contain 'ClassName', 'ClassXml', and 'Properties' keys.</param>
    /// <returns>A Hashtable representation of the XML element.</returns>
    public static Hashtable ToHashtable(XElement element, Hashtable? xmlMetadata = null)
    {
        if (xmlMetadata == null)
        {
            return ToHashtableInternal(element, null, []);
        }

        // Convert hashtable metadata to OpenApiXml object for class-level metadata
        OpenApiXmlModel? classXml = null;
        if (xmlMetadata["ClassXml"] is Hashtable classXmlHash)
        {
            classXml = HashtableToOpenApiXml(classXmlHash);
        }

        // Convert property metadata hashtables to OpenApiXml objects
        var propertyModels = new Dictionary<string, OpenApiXmlModel>();
        if (xmlMetadata["Properties"] is Hashtable propsHash)
        {
            foreach (DictionaryEntry entry in propsHash)
            {
                if (entry.Key is string propName && entry.Value is Hashtable propXmlHash)
                {
                    var propXml = HashtableToOpenApiXml(propXmlHash);
                    if (propXml != null)
                    {
                        propertyModels[propName] = propXml;
                    }
                }
            }
        }

        return ToHashtableInternal(element, classXml, propertyModels);
    }

    /// <summary>
    /// Converts a hashtable representation of OpenAPI XML metadata to an OpenApiXml object.
    /// </summary>
    /// <param name="hash">Hashtable containing Name, Namespace, Prefix, Attribute, and/or Wrapped keys.</param>
    /// <returns>An OpenApiXml object with the specified properties, or null if the hashtable is empty/invalid.</returns>
    private static OpenApiXmlModel? HashtableToOpenApiXml(Hashtable hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        if (hash.Count == 0)
        {
            return null;
        }

        var xml = new OpenApiXmlModel();
        ApplyOpenApiXmlName(hash, xml);
        ApplyOpenApiXmlNamespace(hash, xml);
        ApplyOpenApiXmlPrefix(hash, xml);
        ApplyOpenApiXmlNodeType(hash, xml);
        return xml;
    }

    /// <summary>
    /// Applies the OpenAPI XML <c>name</c> value from a metadata hashtable.
    /// </summary>
    /// <param name="hash">Metadata hashtable.</param>
    /// <param name="xml">Target OpenAPI XML object.</param>
    private static void ApplyOpenApiXmlName(Hashtable hash, OpenApiXmlModel xml)
    {
        if (hash["Name"] is string name)
        {
            xml.Name = name;
        }
    }

    /// <summary>
    /// Applies the OpenAPI XML <c>namespace</c> value from a metadata hashtable.
    /// </summary>
    /// <param name="hash">Metadata hashtable.</param>
    /// <param name="xml">Target OpenAPI XML object.</param>
    private static void ApplyOpenApiXmlNamespace(Hashtable hash, OpenApiXmlModel xml)
    {
        if (hash["Namespace"] is string ns && !string.IsNullOrWhiteSpace(ns))
        {
            xml.Namespace = new Uri(ns);
        }
    }

    /// <summary>
    /// Applies the OpenAPI XML <c>prefix</c> value from a metadata hashtable.
    /// </summary>
    /// <param name="hash">Metadata hashtable.</param>
    /// <param name="xml">Target OpenAPI XML object.</param>
    private static void ApplyOpenApiXmlPrefix(Hashtable hash, OpenApiXmlModel xml)
    {
        if (hash["Prefix"] is string prefix)
        {
            xml.Prefix = prefix;
        }
    }

    /// <summary>
    /// Applies the OpenAPI XML node type (<c>attribute</c> vs element) based on metadata flags.
    /// </summary>
    /// <param name="hash">Metadata hashtable.</param>
    /// <param name="xml">Target OpenAPI XML object.</param>
    private static void ApplyOpenApiXmlNodeType(Hashtable hash, OpenApiXmlModel xml)
    {
        if (hash["Attribute"] is bool isAttribute && isAttribute)
        {
            xml.NodeType = OpenApiXmlNodeType.Attribute;
            return;
        }

        if (hash["Wrapped"] is bool isWrapped && isWrapped)
        {
            xml.NodeType = OpenApiXmlNodeType.Element;
        }
    }

    /// <summary>
    /// Internal recursive method to convert an <see cref="XElement"/> into a <see cref="Hashtable"/> with OpenAPI XML model support.
    /// </summary>
    /// <param name="element">The XML element to convert.</param>
    /// <param name="xmlModel">OpenAPI XML model metadata for the current element.</param>
    /// <param name="propertyModels">Dictionary of property-specific XML models for child elements.</param>
    /// <returns>A Hashtable representation of the XML element.</returns>
    private static Hashtable ToHashtableInternal(XElement element, OpenApiXmlModel? xmlModel, Dictionary<string, OpenApiXmlModel> propertyModels)
    {
        var elementName = GetEffectiveElementName(element, xmlModel);

        if (TryConvertNilElement(element, elementName, out var nilResult))
        {
            return nilResult;
        }

        var table = new Hashtable();
        AddAttributesToTable(element, table, propertyModels);

        if (TryAddLeafValue(element, elementName, table))
        {
            return table;
        }

        table[elementName] = ConvertChildElements(element, propertyModels);
        return table;
    }

    /// <summary>
    /// Gets the effective name for an element, honoring OpenAPI <c>xml.name</c> overrides.
    /// </summary>
    /// <param name="element">The element whose name is being resolved.</param>
    /// <param name="xmlModel">The OpenAPI XML model for the current element.</param>
    /// <returns>The resolved element name.</returns>
    private static string GetEffectiveElementName(XElement element, OpenApiXmlModel? xmlModel)
        => xmlModel?.Name ?? element.Name.LocalName;

    /// <summary>
    /// Detects <c>xsi:nil="true"</c> and returns the null-valued hashtable representation.
    /// </summary>
    /// <param name="element">The element to inspect for <c>xsi:nil</c>.</param>
    /// <param name="elementName">The effective element name to use as the hashtable key.</param>
    /// <param name="result">The null-valued hashtable when <c>xsi:nil</c> is present; otherwise a default value.</param>
    /// <returns><c>true</c> when <c>xsi:nil="true"</c> is present; otherwise <c>false</c>.</returns>
    private static bool TryConvertNilElement(XElement element, string elementName, out Hashtable result)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.NamespaceName == xsi.NamespaceName
                && attr.Name.LocalName == "nil"
                && string.Equals(attr.Value, "true", StringComparison.OrdinalIgnoreCase))
            {
                result = new Hashtable { [elementName] = null };
                return true;
            }
        }

        result = default!;
        return false;
    }

    /// <summary>
    /// Adds element attributes to the target hashtable, honoring OpenAPI <c>xml.attribute</c> mappings.
    /// </summary>
    /// <param name="element">The element whose attributes are being read.</param>
    /// <param name="table">The hashtable to populate.</param>
    /// <param name="propertyModels">Property-level OpenAPI XML models used to map attribute names to property keys.</param>
    private static void AddAttributesToTable(XElement element, Hashtable table, Dictionary<string, OpenApiXmlModel> propertyModels)
    {
        foreach (var attr in element.Attributes())
        {
            var attrKey = FindPropertyKeyForAttribute(attr.Name.LocalName, propertyModels);
            if (attrKey is not null)
            {
                table[attrKey] = attr.Value;
                continue;
            }

            table["@" + attr.Name.LocalName] = attr.Value;
        }
    }

    /// <summary>
    /// Converts a leaf element (no child elements) by storing its string value under the effective element name.
    /// </summary>
    /// <param name="element">The element to evaluate.</param>
    /// <param name="elementName">The effective element name.</param>
    /// <param name="table">The hashtable to populate.</param>
    /// <returns><c>true</c> when the element is a leaf; otherwise <c>false</c>.</returns>
    private static bool TryAddLeafValue(XElement element, string elementName, Hashtable table)
    {
        if (element.HasElements)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(element.Value))
        {
            table[elementName] = element.Value;
        }

        return true;
    }

    /// <summary>
    /// Converts child elements into a nested hashtable, merging repeated keys into a list.
    /// </summary>
    /// <param name="element">The parent element whose children are being converted.</param>
    /// <param name="propertyModels">Property-level OpenAPI XML models used to map element names to property keys.</param>
    /// <returns>A hashtable of child values.</returns>
    private static Hashtable ConvertChildElements(XElement element, Dictionary<string, OpenApiXmlModel> propertyModels)
    {
        var childMap = new Hashtable();

        foreach (var child in element.Elements())
        {
            var childLocalName = child.Name.LocalName;

            // Map the XML element name back to the *property key* (like attributes do) so callers can bind
            // the resulting hashtable to CLR/PowerShell classes.
            var childPropertyKey = FindPropertyKeyForElement(childLocalName, propertyModels);
            var childModel = childPropertyKey != null ? propertyModels[childPropertyKey] : null;
            var childElementName = childModel?.Name ?? childLocalName;

            var childValue = ToHashtableInternal(child, childModel, []);
            var valueToStore = childValue[childElementName];

            var keyToStore = childPropertyKey ?? childElementName;
            AddOrAppendChildValue(childMap, keyToStore, valueToStore);
        }

        return childMap;
    }

    /// <summary>
    /// Adds a child value to the map, converting repeated keys into a list (allowing null values).
    /// </summary>
    /// <param name="childMap">Target map of child values.</param>
    /// <param name="key">Key to store under.</param>
    /// <param name="value">Value to store.</param>
    private static void AddOrAppendChildValue(Hashtable childMap, string key, object? value)
    {
        if (!childMap.ContainsKey(key))
        {
            childMap[key] = value;
            return;
        }

        if (childMap[key] is List<object?> list)
        {
            list.Add(value);
            return;
        }

        childMap[key] = new List<object?>
        {
            childMap[key],
            value
        };
    }

    /// <summary>
    /// Finds the property key that corresponds to an XML attribute based on OpenAPI XML models.
    /// </summary>
    /// <param name="attributeName">The XML attribute name.</param>
    /// <param name="propertyModels">Dictionary of property-specific XML models.</param>
    /// <returns>The property key if found; otherwise, null.</returns>
    private static string? FindPropertyKeyForAttribute(string attributeName, Dictionary<string, OpenApiXmlModel> propertyModels)
    {
        foreach (var kvp in propertyModels)
        {
            var model = kvp.Value;
            // Check if this property is marked as an attribute and matches the name
            if (model.NodeType == OpenApiXmlNodeType.Attribute)
            {
                var expectedName = model.Name ?? kvp.Key;
                if (string.Equals(expectedName, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key; // Return the original property name
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the property key that corresponds to an XML element name based on OpenAPI XML models.
    /// </summary>
    /// <param name="elementName">The XML element local name.</param>
    /// <param name="propertyModels">Dictionary of property-specific XML models.</param>
    /// <returns>The property key if found; otherwise, null.</returns>
    private static string? FindPropertyKeyForElement(string elementName, Dictionary<string, OpenApiXmlModel> propertyModels)
    {
        // Fast path: property name matches element name.
        foreach (var kvp in propertyModels)
        {
            if (string.Equals(kvp.Key, elementName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        // Match using model.Name override.
        foreach (var kvp in propertyModels)
        {
            var model = kvp.Value;
            var expectedName = model.Name ?? kvp.Key;
            if (string.Equals(expectedName, elementName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        return null;
    }
}
