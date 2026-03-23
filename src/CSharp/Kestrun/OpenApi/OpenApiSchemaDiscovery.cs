// OpenApiSchemaDiscovery.cs
using Kestrun.Forms;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper to discover OpenAPI schema types in loaded assemblies.
/// </summary>
public static class OpenApiSchemaDiscovery
{
    /// <summary>
    /// Discover all OpenAPI component types from dynamic PowerShell classes.
    /// Scans all loaded assemblies for classes decorated with OpenApiModelKindAttribute
    /// or having any OpenApi* attributes.
    /// </summary>
    /// <returns> The discovered OpenAPI component types.</returns>
    public static OpenApiComponentSet GetOpenApiTypesAuto()
    {
        var assemblies = GetRelevantAssemblies();
        return new OpenApiComponentSet
        {
            SchemaTypes = GetSchemaTypes(assemblies)
        };
    }

    private static System.Reflection.Assembly[] GetRelevantAssemblies()
    {
        return [.. AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName is not null &&
                    (a.FullName.Contains("PowerShell Class Assembly") ||
                        a.FullName.StartsWith("Kestrun")))];
    }

    private static Type[] GetSchemaTypes(System.Reflection.Assembly[] assemblies)
    {
        var primitivesAssembly = typeof(OpenApiString).Assembly;

        return [.. assemblies.SelectMany(GetLoadableTypes)
            .Where(t => t.IsClass && !t.IsAbstract &&
                    t.IsDefined(typeof(OpenApiSchemaComponent), true) &&
                    !IsFormPayloadBaseType(t) &&
                    // Exclude built-in OpenApi* primitives from auto-discovered components,
                    // but keep user-defined schema components that inherit those primitives.
                    !(t.Assembly == primitivesAssembly && typeof(IOpenApiScalar).IsAssignableFrom(t)))];
    }

    /// <summary>
    /// Returns all loadable types from an assembly, even when some types fail to load.
    /// </summary>
    /// <param name="assembly">Assembly to enumerate types from.</param>
    /// <returns>Loadable types discovered in the assembly.</returns>
    private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }

    private static bool IsFormPayloadBaseType(Type t)
    {
        // Avoid emitting base form payload schemas unless explicitly referenced.
        return t == typeof(KrFormData)
            || t == typeof(KrMultipart);
    }
}
