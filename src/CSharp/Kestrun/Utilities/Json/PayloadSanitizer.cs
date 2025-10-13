using System.Collections;
using System.Management.Automation;
using System.Runtime.CompilerServices;
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

    private static object? Sanitize(object? value, HashSet<object> visited, int depth)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (type.IsValueType || value is string)
        {
            // Normalize some known value types that don't serialize as desired by default
            if (value is TimeSpan ts)
            {
                // Use constant (round-trippable) format 'c' (e.g., "1.02:03:04.0050000")
                return ts.ToString("c", CultureInfo.InvariantCulture);
            }
            return value;
        }

        if (!TryAddVisited(value, visited))
        {
            return "[Circular]";
        }

        // Unwrap PowerShell objects safely
        if (value is PSObject pso)
        {
            if (IsPSCustomObject(pso))
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in pso.Properties)
                {
                    // Skip meta/special members that cause cycles
                    if (IsSpecialMember(prop.Name))
                    {
                        continue;
                    }
                    dict[prop.Name] = Sanitize(prop.Value, visited, depth + 1);
                }
                return dict;
            }

            // Otherwise unwrap to base object and continue
            return Sanitize(pso.BaseObject, visited, depth + 1);
        }

        if (value is IDictionary idict)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                dict[key] = Sanitize(entry.Value, visited, depth + 1);
            }
            return dict;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(Sanitize(item, visited, depth + 1));
            }
            return list;
        }

        // Friendly representations for a few tricky types
        if (value is Type t)
        {
            return t.FullName;
        }
        if (value is Delegate del)
        {
            return del.Method?.Name ?? del.GetType().Name;
        }
        if (value is Exception ex)
        {
            return new
            {
                Type = ex.GetType().FullName,
                ex.Message,
                ex.StackTrace
            };
        }

        // Fallback: return as-is; System.Text.Json will attempt to serialize public properties
        return value;
    }

    private static bool TryAddVisited(object o, HashSet<object> visited) =>
        // Strings and value types are safe (immutable/value)
        o is string || o.GetType().IsValueType || visited.Add(o);

    private static bool IsPSCustomObject(PSObject pso)
        => pso.BaseObject is PSCustomObject || pso.TypeNames.Contains("System.Management.Automation.PSCustomObject");

    private static bool IsSpecialMember(string name)
        => name is "PSObject" or "BaseObject" or "Members" or "ImmediateBaseObject" or "Properties" or "TypeNames" or "Methods";
}

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
