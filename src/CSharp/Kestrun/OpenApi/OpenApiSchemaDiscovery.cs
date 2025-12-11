// OpenApiSchemaDiscovery.cs
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
            ParameterTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiParameterComponent)),
            SchemaTypes = GetSchemaTypes(assemblies),
            ResponseTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiResponseComponent)),
            HeaderTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiHeaderComponent)),
            ExampleTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiExampleComponent)),
            RequestBodyTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiRequestBodyComponent)),
#if EXTENDED_OPENAPI
            SecuritySchemeTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiSecuritySchemeComponent)),
            LinkTypes = GetTypesWithAttribute(assemblies, typeof(OpenApiLinkComponent)),
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

    private static Type[] GetTypesWithAttribute(System.Reflection.Assembly[] assemblies, Type attributeType)
    {
        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsDefined(attributeType, true))];
    }

    private static Type[] GetSchemaTypes(System.Reflection.Assembly[] assemblies)
    {
        return [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    !typeof(IOpenApiType).IsAssignableFrom(t) &&
                    t.IsDefined(typeof(OpenApiSchemaComponent), true))];
    }

#if EXTENDED_OPENAPI
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
