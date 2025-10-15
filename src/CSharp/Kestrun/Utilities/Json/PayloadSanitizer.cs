using System.Collections;
using System.Management.Automation;
using System.Globalization;

namespace Kestrun.Utilities.Json;

/// <summary>
/// Utilities to sanitize arbitrary payloads (especially PowerShell objects) into JSON-friendly shapes
/// for System.Text.Json/SignalR serialization without reference cycles.
/// </summary>
public static class PayloadSanitizer
{
    /// <summary>
    /// Returns a sanitized version of the provided value suitable for JSON serialization.
    /// - Unwraps PSObject/PSCustomObject into dictionaries
    /// - Converts IDictionary into Dictionary&lt;string, object?&gt;
    /// - Converts IEnumerable into List&lt;object?&gt;
    /// - Replaces circular references with the string "[Circular]"
    /// </summary>
    public static object? Sanitize(object? value)
        => Sanitize(value, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);

    /// <summary>
    /// Internal recursive sanitizer with cycle detection.
    /// </summary>
    /// <param name="value">The value to sanitize.</param>
    /// <param name="visited">The set of visited objects.</param>
    /// <param name="depth">The current depth in the object graph.</param>
    /// <returns>The sanitized value.</returns>
    private static object? Sanitize(object? value, HashSet<object> visited, int depth)
    {
        if (value is null)
        {
            return null;
        }

        // Fast-path for scalars and special normalization
        if (IsSimpleScalar(value, out var simple))
        {
            return simple;
        }

        // Prevent cycles for reference types
        if (!TryAddVisited(value, visited))
        {
            return "[Circular]";
        }

        // Handlers for common composite/PowerShell shapes
        if (TryHandlePowerShellObject(value, visited, depth, out var psResult))
        {
            return psResult;
        }
        if (TryHandleDictionary(value, visited, depth, out var dictResult))
        {
            return dictResult;
        }
        if (TryHandleEnumerable(value, visited, depth, out var listResult))
        {
            return listResult;
        }

        // Friendly representations for tricky types
        if (TryHandleFriendlyTypes(value, out var friendly))
        {
            return friendly;
        }

        // Fallback: let System.Text.Json serialize public properties as-is
        return value;
    }

    private static bool IsSimpleScalar(object value, out object? normalized)
    {
        // Treat value types and strings as simple scalars, with a special case for TimeSpan
        if (value is string)
        {
            normalized = value;
            return true;
        }

        var type = value.GetType();
        if (type.IsValueType)
        {
            if (value is TimeSpan ts)
            {
                normalized = ts.ToString("c", CultureInfo.InvariantCulture);
                return true;
            }

            normalized = value;
            return true;
        }

        normalized = null;
        return false;
    }

    private static bool TryHandlePowerShellObject(object value, HashSet<object> visited, int depth, out object? result)
    {
        if (value is PSObject pso)
        {
            if (IsPSCustomObject(pso))
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in pso.Properties)
                {
                    if (IsSpecialMember(prop.Name))
                    {
                        continue; // Skip meta/special members that cause cycles
                    }
                    dict[prop.Name] = Sanitize(prop.Value, visited, depth + 1);
                }
                result = dict;
                return true;
            }

            // Otherwise unwrap to base object and continue
            result = Sanitize(pso.BaseObject, visited, depth + 1);
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryHandleDictionary(object value, HashSet<object> visited, int depth, out object? result)
    {
        if (value is IDictionary idict)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                dict[key] = Sanitize(entry.Value, visited, depth + 1);
            }
            result = dict;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryHandleEnumerable(object value, HashSet<object> visited, int depth, out object? result)
    {
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(Sanitize(item, visited, depth + 1));
            }
            result = list;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryHandleFriendlyTypes(object value, out object? result)
    {
        switch (value)
        {
            case Type t:
                result = t.FullName;
                return true;
            case Delegate del:
                result = del.Method?.Name ?? del.GetType().Name;
                return true;
            case Exception ex:
                result = new
                {
                    Type = ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace
                };
                return true;
            default:
                result = null;
                return false;
        }
    }

    private static bool TryAddVisited(object o, HashSet<object> visited) =>
        // Strings and value types are safe (immutable/value)
        o is string || o.GetType().IsValueType || visited.Add(o);

    private static bool IsPSCustomObject(PSObject pso)
        => pso.BaseObject is PSCustomObject || pso.TypeNames.Contains("System.Management.Automation.PSCustomObject");

    private static bool IsSpecialMember(string name)
        => name is "PSObject" or "BaseObject" or "Members" or "ImmediateBaseObject" or "Properties" or "TypeNames" or "Methods";
}
