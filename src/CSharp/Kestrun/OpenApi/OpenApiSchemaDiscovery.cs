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
                   (t.GetCustomAttributes(typeof(OpenApiSchemaComponent), true).Length != 0 ||
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

            LinkTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                    (t.GetCustomAttributes(typeof(OpenApiLinkComponent), true).Length != 0))],
            /*         CallbackTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
                       .Where(t => t.IsClass && !t.IsAbstract &&
                           t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                            .OfType<OpenApiModelKindAttribute>()
                            .Any(a => a.Kind == OpenApiModelKind.Callback))],
                   PathItemTypes = [.. assemblies.SelectMany(asm => asm.GetTypes())
                       .Where(t => t.IsClass && !t.IsAbstract &&
                           t.GetCustomAttributes(typeof(OpenApiModelKindAttribute), true)
                            .OfType<OpenApiModelKindAttribute>()
                            .Any(a => a.Kind == OpenApiModelKind.PathItem))]
                            */
        };
    }

}
