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
            SchemaTypes = GetSchemaTypes(assemblies),
#if EXTENDED_OPENAPI
            RequestBodyTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiRequestBodyComponentAttribute)),
            ResponseTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiResponseComponentAttribute)),
            ParameterTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiParameterComponentAttribute)),
            HeaderTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiHeaderAttribute)),
            SecuritySchemeTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiSecuritySchemeComponent)),
            CallbackTypes = GetTypesWithKind(assemblies, OpenApiModelKind.Callback),
            PathItemTypes = GetTypesWithKind(assemblies, OpenApiModelKind.PathItem)
#endif
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

        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    t.IsDefined(typeof(OpenApiSchemaComponent), true) &&
                    !IsFormPayloadBaseType(t) &&
                    // Exclude built-in OpenApi* primitives from auto-discovered components,
                    // but keep user-defined schema components that inherit those primitives.
                    !(t.Assembly == primitivesAssembly && typeof(IOpenApiScalar).IsAssignableFrom(t)))];
    }

    private static bool IsFormPayloadBaseType(Type t)
    {
        // Avoid emitting base form payload schemas unless explicitly referenced.
        return t == typeof(KrFormData)
            || t == typeof(KrMultipart);
    }

#if EXTENDED_OPENAPI

   private static Type[] GetTypesWithAttribute(System.Reflection.Assembly[] assemblies, Type attributeType)
    {
        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsDefined(attributeType, true))];
    }
    private static Type[] GetTypesWithKind(System.Reflection.Assembly[] assemblies, OpenApiModelKind kind)
    {
        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                 .OfType<OpenApiModelKindAttribute>()
                 .Any(a => a.Kind == kind))];
    }
#endif
}
