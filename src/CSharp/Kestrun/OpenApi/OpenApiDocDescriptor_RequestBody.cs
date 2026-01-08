using System.Reflection;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds request body components from the specified type.
    /// </summary>
    /// <param name="t">The type to build request bodies for.</param>
    private void BuildRequestBodies(Type t)
    {
        Document.Components!.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
        var componentSchema = BuildSchemaForType(t);
        var requestBody = new OpenApiRequestBody();
        // Apply request body component attribute if present
        var name = ApplyRequestBodyComponent(t.GetCustomAttribute<OpenApiRequestBodyComponent>(), requestBody, componentSchema);

        // Apply example references if any (handle multiple attributes)
        var exampleRefs = t.GetCustomAttributes<OpenApiExampleRefAttribute>();
        foreach (var exRef in exampleRefs)
        {
            ApplyRequestBodyExampleRef(exRef, requestBody);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = t.Name; // fallback to type name if no explicit key was provided
        }
        // Register the request body component
        _ = Document.Components!.RequestBodies.TryAdd(name, requestBody);
    }

    /// <summary>
    /// Applies the request body component attribute to the request body.
    /// </summary>
    /// <param name="bodyAttribute"> The request body component attribute to apply.</param>
    /// <param name="requestBody"> The request body to apply the attribute to.</param>
    /// <param name="schema"> The schema to associate with the request body.</param>
    /// <returns>The name of the request body component if explicitly specified; otherwise, null.</returns>
    private static string? ApplyRequestBodyComponent(OpenApiRequestBodyComponent? bodyAttribute, OpenApiRequestBody requestBody, IOpenApiSchema schema)
    {
        if (bodyAttribute is null)
        {
            return null;
        }
        var name = string.Empty;
        var explicitKey = GetKeyOverride(bodyAttribute);
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            name = explicitKey;
        }

        if (bodyAttribute.Description is not null)
        {
            requestBody.Description = bodyAttribute.Description;
        }
        requestBody.Required |= bodyAttribute.Required;
        requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);

        var mediaType = new OpenApiMediaType { Schema = schema };
        if (bodyAttribute.Example is not null)
        {
            mediaType.Example = ToNode(bodyAttribute.Example);
        }

        foreach (var ct in bodyAttribute.ContentType)
        {
            requestBody.Content[ct] = mediaType;
        }

        return name;
    }

    /// <summary>
    /// Applies example references to the request body.
    /// </summary>
    /// <param name="exRef">The example reference attribute to apply.</param>
    /// <param name="requestBody">The request body to apply the example references to.</param>
    private void ApplyRequestBodyExampleRef(OpenApiExampleRefAttribute? exRef, OpenApiRequestBody requestBody)
    {
        if (exRef is null)
        {
            return;
        }
        requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        var targets = ResolveExampleContentTypes(exRef, requestBody);
        foreach (var ct in targets)
        {
            var mediaType = requestBody.Content.TryGetValue(ct, out var existing)
                ? existing
                : (requestBody.Content[ct] = new OpenApiMediaType());

            if (mediaType is not OpenApiMediaType concreteMedia)
            {
                throw new InvalidOperationException($"Expected OpenApiMediaType for content type '{ct}', got '{mediaType.GetType().FullName}'.");
            }

            concreteMedia.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            concreteMedia.Examples[exRef.Key] = exRef.Inline
                ? CloneExampleOrThrow(exRef.ReferenceId)
                : new OpenApiExampleReference(exRef.ReferenceId);
        }
    }

    /// <summary>
    /// Resolves the content types to apply example references to.
    /// </summary>
    /// <param name="exRef">The example reference attribute.</param>
    /// <param name="requestBody">The request body.</param>
    /// <returns>The list of content types to apply the example references to.</returns>
    private static IEnumerable<string> ResolveExampleContentTypes(OpenApiExampleRefAttribute exRef, OpenApiRequestBody requestBody)
    {
        var keys = exRef.ContentType is null ? (requestBody.Content?.Keys ?? Array.Empty<string>()) : exRef.ContentType;
        return keys.Count == 0 ? ["application/json"] : (IEnumerable<string>)keys;
    }

    /// <summary>
    /// Clones an example from components or throws if not found.
    /// </summary>
    /// <param name="referenceId"> The reference ID of the example to clone.</param>
    /// <returns>The cloned example.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the example reference cannot be found or is not an OpenApiExample.</exception>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private IOpenApiExample CloneExampleOrThrow(string referenceId)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        return Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(referenceId, out var value)
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it was not found in components.")
            : value is not OpenApiExample example
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it is not an OpenApiExample.")
            : (IOpenApiExample)example.Clone();
    }
}
