// OpenApiSchemaDiscovery.cs
using System.Reflection;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper to discover OpenAPI schema types in loaded assemblies.
/// </summary>
public static class OpenApiSchemaDiscovery
{
    private const string ModelKindAttrName = "OpenApiModelKindAttribute";
    private const string SchemaAttrName = "OpenApiSchemaAttribute";
    private const string ModelKindEnumName = "OpenApiModelKind";
    private const string ModelKindSchemaName = "Schema";

    /// <summary>
    /// Discover schema types (plus referenced complex types and enums).
    /// Works with or without a namespace prefix filter. If <paramref name="namespacePrefix"/> is null,
    /// scans *all* loaded assemblies, including dynamic PowerShell class assemblies.
    /// </summary>
    /// <param name="namespacePrefix">The namespace prefix to filter by, or null to include all namespaces.</param>
    /// <returns>All discovered schema types.</returns>
    public static Type[] GetOpenApiSchemaTypes(string namespacePrefix)
    {
        var types = GetOpenApiSchemaTypes((Type?)null);
        return (namespacePrefix == null)
            ? types
            : [.. types.Where(t => t.Namespace != null && t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal))];
    }

    /// <summary>
    /// Discover parameter types.
    /// Works with or without a hint. If <paramref name="hintType"/> is null,
    /// scans *all* loaded assemblies, including dynamic PowerShell class assemblies.
    /// </summary>
    /// <param name="hintType">The hint type to limit assembly scanning, or null to scan all loaded assemblies.</param>
    /// <returns>All discovered parameter types.</returns>
    public static Type[] GetOpenApiParameterTypes(Type? hintType = null)
    {
        var assemblies = (hintType != null)
            ? [hintType.Assembly]
            : AppDomain.CurrentDomain.GetAssemblies();

        var allClasses = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsClass: true } && !t.IsAbstract)
            .ToArray();

        var paramTypes = allClasses
            .Where(t => IsParameterKind(t) || HasParameterDecoratedProperty(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

        return paramTypes;
    }

    /// <summary>
    /// Discover schema types (plus referenced complex types and enums).
    /// Works with or without a hint. If <paramref name="hintType"/> is null,
    /// scans *all* loaded assemblies, including dynamic PowerShell class assemblies.
    /// </summary>
    public static Type[] GetOpenApiSchemaTypes(Type? hintType = null)
    {
        var assemblies = (hintType != null)
            ? [hintType.Assembly]
            : AppDomain.CurrentDomain.GetAssemblies(); // <-- include dynamic assemblies too

        var allClasses = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsClass: true } && !t.IsAbstract)
            .ToArray();

        var seed = allClasses.Where(t => IsSchemaKind(t) || HasClassSchema(t)).ToArray();

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
                if (element == null)
                {
                    continue;
                }

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

        return [.. seen.OrderBy(x => x.FullName, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Discover all OpenAPI component types from dynamic PowerShell classes.
    /// Scans all loaded assemblies for classes decorated with OpenApiModelKindAttribute
    /// or having any OpenApi* attributes.
    /// </summary>
    /// <returns> </returns>
    public static OpenApiComponentSet GetOpenApiTypesAuto()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName is not null &&
                    (a.FullName.Contains("PowerShell Class Assembly") ||
                        a.FullName.StartsWith("Kestrun")))
            .ToArray();
        return new OpenApiComponentSet()
        {

            ParameterTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    (t.GetCustomAttributes(typeof(OpenApiParameterComponent), true).Length != 0
                    ))],

            SchemaTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                   (t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                     .OfType<OpenApiModelKindAttribute>()
                     .Any(a => a.Kind == OpenApiModelKind.Schema) ||
                    t.GetCustomAttributes(typeof(OpenApiRequestBodyComponent), true).Length != 0))],
            // Use similar logic for Response types
            ResponseTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                   (t.GetCustomAttributes(typeof(OpenApiResponseComponent), true).Length != 0
                   ))
                    ],
            // Use similar logic for Header types
            HeaderTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    (t.GetCustomAttributes(typeof(OpenApiHeaderComponent), true).Length != 0
                    ))],
            ExampleTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                   ( t.GetCustomAttributes(typeof(OpenApiExampleComponent), true).Length != 0))],
            RequestBodyTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    (t.GetCustomAttributes(typeof(OpenApiRequestBodyComponent), true).Length != 0))],

            // Use only OpenApiModelKindAttribute to avoid compile-time deps on attribute types that may not exist yet
            LinkTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract &&
                    t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                     .OfType<OpenApiModelKindAttribute>()
                     .Any(a => a.Kind == OpenApiModelKind.Link))],
            CallbackTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract &&
                    t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                     .OfType<OpenApiModelKindAttribute>()
                     .Any(a => a.Kind == OpenApiModelKind.Callback))],
            PathItemTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract &&
                    t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                     .OfType<OpenApiModelKindAttribute>()
                     .Any(a => a.Kind == OpenApiModelKind.PathItem))]
        };
    }

    /// <summary>
    /// Discover OpenAPI schema types from dynamic PowerShell classes.
    /// Scans all loaded assemblies for classes decorated with OpenApiModelKind(Schema)
    /// or having any OpenApiSchema attributes.
    /// </summary>
    /// <returns>All discovered schema types.</returns>
    public static IEnumerable<Type> GetOpenApiSchemaTypesAuto()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName is not null &&
                    (a.FullName.Contains("PowerShell Class Assembly") ||
                        a.FullName.StartsWith("Kestrun")))
            .ToArray();


        var result = new HashSet<Type>();

        foreach (var asm in assemblies)
        {
            foreach (var t in asm.GetTypes())
            {
                if (!t.IsClass || t.IsAbstract)
                {
                    continue;
                }

                // PowerShell class decorated with [OpenApiModelKind(Schema)]
                if (t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                      .OfType<OpenApiModelKindAttribute>()
                      .Any(a => a.Kind == OpenApiModelKind.Schema))
                {
                    _ = result.Add(t);
                    continue;
                }

                // Or class has any [OpenApiSchema] attributes
                if (t.GetCustomAttributes(typeof(OpenApiSchemaAttribute), true).Length != 0)
                {
                    _ = result.Add(t);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Discover parameter types from dynamic PowerShell classes.
    /// Scans all loaded assemblies for classes decorated with OpenApiModelKind(Parameters)
    /// or having any OpenApiParameter attributes.
    /// </summary>
    /// <returns>The discovered parameter types.</returns>
    public static IEnumerable<Type> GetOpenApiParameterTypesAuto()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName is not null &&
                    (a.FullName.Contains("PowerShell Class Assembly") ||
                        a.FullName.StartsWith("Kestrun")))
            .ToArray();

        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                   (t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                     .OfType<OpenApiModelKindAttribute>()
                     .Any(a => a.Kind == OpenApiModelKind.Parameters) ||
                    t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Any(p => p.GetCustomAttributes(typeof(OpenApiParameterAttribute), true).Length != 0)))];
    }

    // ----- helpers -----

    /// <summary>
    /// Safely get types from an assembly, handling ReflectionTypeLoadException.
    /// </summary>
    /// <param name="asm">The assembly to scan.</param>
    /// <returns></returns>
    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        catch { return []; }
    }

    /// <summary>
    /// Check if type has OpenApiModelKindAttribute with Kind == OpenApiModelKind.Schema
    /// </summary>
    /// <param name="t">The type to check.</param>
    /// <returns></returns>
    private static bool IsSchemaKind(Type t)
    {
        foreach (var a in t.GetCustomAttributes(inherit: false))
        {
            var at = a.GetType();
            if (!string.Equals(at.Name, ModelKindAttrName, StringComparison.Ordinal))
            {
                continue;
            }

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

            if (!string.Equals(kindValue.GetType().Name, ModelKindEnumName, StringComparison.Ordinal))
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

    private static bool HasClassSchema(Type t) =>
        t.GetCustomAttributes(inherit: false)
         .Any(a => string.Equals(a.GetType().Name, SchemaAttrName, StringComparison.Ordinal));

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
        t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);

    // add near your other constants
    private const string ParameterAttrName = "OpenApiParameterAttribute";
    private const string ModelKindParamsName = "Parameters";

    // helper: class is [OpenApiModelKind(Parameters)]
    private static bool IsParameterKind(Type t)
    {
        foreach (var a in t.GetCustomAttributes(inherit: false))
        {
            var at = a.GetType();
            if (!string.Equals(at.Name, ModelKindAttrName, StringComparison.Ordinal))
            {
                continue;
            }

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

            if (!string.Equals(kindValue.GetType().Name, ModelKindEnumName, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(kindValue.ToString(), ModelKindParamsName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    // helper: class has at least one property with [OpenApiParameter(...)]
    private static bool HasParameterDecoratedProperty(Type t)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            var has = p.GetCustomAttributes(inherit: false)
                       .Any(a => string.Equals(a.GetType().Name, ParameterAttrName, StringComparison.Ordinal));
            if (has)
            {
                return true;
            }
        }
        return false;
    }




}
