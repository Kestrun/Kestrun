using System.Reflection;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper to discover all OpenAPI schema types in loaded assemblies.
/// </summary>
public static class OpenApiSchemaDiscovery
{
    // Attribute type names (no hard reference required)
    private const string ModelKindAttrName = "OpenApiModelKindAttribute";
    private const string SchemaAttrName = "OpenApiSchemaAttribute";
    private const string ModelKindEnumName = "OpenApiModelKind";
    private const string ModelKindSchemaName = "Schema";

    /// <summary>
    /// Discover all schema types (and referenced complex/enums) using the same rules as the PS function.
    /// </summary>
    /// <param name="hintType">Optional: restrict search to this type's Assembly.</param>
    /// <returns>Distinct, sorted list of System.Type.</returns>
    public static Type[] GetOpenApiSchemaTypes(Type? hintType = null)
    {
        var assemblies = (hintType != null)
            ? new[] { hintType.Assembly }
            : [.. AppDomain.CurrentDomain.GetAssemblies().Where(IsUsableAssembly)];

        // 1) all non-abstract public classes from those assemblies
        var allClasses = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToArray();

        // 2) seed: classes marked as [OpenApiModelKind(Schema)] OR with class-level [OpenApiSchema]
        var seed = allClasses.Where(t => IsSchemaKind(t) || HasClassSchema(t)).ToArray();

        // 3) BFS: include transitively referenced complex types + enums
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();
        foreach (var t in seed)
        {
            if (seen.Add(t))
            {
                queue.Enqueue(t);
            }
        }

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();

            foreach (var pt in GetPublicPropertyTypes(t))
            {
                var element = pt.IsArray ? pt.GetElementType()! : pt;

                if (element.IsEnum)
                {
                    _ = seen.Add(element);
                    continue;
                }

                if (!IsPrimitiveLike(element) && element.IsClass && !element.IsAbstract)
                {
                    if (seen.Add(element))
                    {
                        queue.Enqueue(element);
                    }
                }
            }
        }

        return [.. seen
            .Where(x => x != null)
            .OrderBy(x => x.FullName, StringComparer.Ordinal)];
    }

    // ------------ helpers ------------

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        catch { return []; }
    }

    private static bool IsUsableAssembly(Assembly asm)
    {
        try
        {
            // skip dynamic/reflection-only
            if (asm.IsDynamic)
            {
                return false;
            }
            _ = asm.FullName; // access to ensure it isn't in a weird state
            return true;
        }
        catch { return false; }
    }

    private static bool IsSchemaKind(Type t)
    {
        foreach (var a in t.GetCustomAttributes(inherit: false))
        {
            var at = a.GetType();
            if (!string.Equals(at.Name, ModelKindAttrName, StringComparison.Ordinal))
            {
                continue;
            }

            // Look for property "Kind" and compare to enum value "Schema"
            var prop = at.GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                continue;
            }

            var kindValue = prop.GetValue(a);
            if (kindValue == null)
            {
                continue;
            }

            var enumType = kindValue.GetType();
            if (!string.Equals(enumType.Name, ModelKindEnumName, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(kindValue.ToString(), ModelKindSchemaName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasClassSchema(Type t)
    {
        // any class-level OpenApiSchemaAttribute
        return t.GetCustomAttributes(inherit: false)
                .Any(a => string.Equals(a.GetType().Name, SchemaAttrName, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> GetPublicPropertyTypes(Type t)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            var pt = p.PropertyType;
            if (pt != null)
            {
                yield return pt;
            }
        }
    }

    private static bool IsPrimitiveLike(Type t) =>
        t.IsPrimitive ||
        t == typeof(string) ||
        t == typeof(decimal) ||
        t == typeof(DateTime) ||
        t == typeof(Guid);
}
