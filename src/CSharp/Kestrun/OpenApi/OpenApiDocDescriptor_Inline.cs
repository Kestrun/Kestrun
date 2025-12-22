using System.Diagnostics.CodeAnalysis;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{

    /// <summary>
    /// Adds an inline example to the OpenAPI document.
    /// </summary>
    /// <param name="name">The name of the inline example.</param>
    /// <param name="example">The inline example to add.</param>
    /// <param name="ifExists">Specifies the behavior if an example with the same name already exists.</param>
    /// <exception cref="InvalidOperationException">Thrown if an example with the same name already exists and ifExists is set to Error.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the ifExists parameter has an invalid value.</exception>
    public void AddInlineExample(
    string name,
    OpenApiExample example,
    OpenApiComponentConflictResolution ifExists = OpenApiComponentConflictResolution.Overwrite)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(example);

        InlineComponents.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);

        switch (ifExists)
        {
            case OpenApiComponentConflictResolution.Error:
                if (!InlineComponents.Examples.TryAdd(name, example))
                {
                    throw new InvalidOperationException($"An inline example named '{name}' already exists.");
                }

                return;

            case OpenApiComponentConflictResolution.Ignore:
                _ = InlineComponents.Examples.TryAdd(name, example);
                return;

            case OpenApiComponentConflictResolution.Overwrite:
                InlineComponents.Examples[name] = example;
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(ifExists), ifExists, null);
        }
    }

    /// <summary>
    /// Adds an inline link to the OpenAPI document.
    /// </summary>
    /// <param name="name">The name of the inline link.</param>
    /// <param name="link">The inline link to add.</param>
    /// <param name="ifExists">Specifies the behavior if a link with the same name already exists.</param>
    /// <exception cref="InvalidOperationException">Thrown if a link with the same name already exists and ifExists is set to Error.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the ifExists parameter has an invalid value.</exception>
    public void AddInlineLink(
        string name,
        OpenApiLink link,
        OpenApiComponentConflictResolution ifExists = OpenApiComponentConflictResolution.Overwrite)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(link);

        InlineComponents.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);

        switch (ifExists)
        {
            case OpenApiComponentConflictResolution.Error:
                if (!InlineComponents.Links.TryAdd(name, link))
                {
                    throw new InvalidOperationException($"An inline link named '{name}' already exists.");
                }

                return;

            case OpenApiComponentConflictResolution.Ignore:
                _ = InlineComponents.Links.TryAdd(name, link);
                return;

            case OpenApiComponentConflictResolution.Overwrite:
                InlineComponents.Links[name] = link;
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(ifExists), ifExists, null);
        }
    }
    /// <summary>
    /// Checks if an element with the specified name exists in the given component kind.
    /// </summary>
    /// <param name="name">The name of the element to check for existence.</param>
    /// <param name="kind">The kind of OpenAPI component to check within.</param>
    /// <returns>True if an element with the specified name exists; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public bool ContainsElementName(string name, OpenApiComponentKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        return kind switch
        {
            OpenApiComponentKind.Schemas => Document.Components?.Schemas?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Responses => Document.Components?.Responses?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Parameters => Document.Components?.Parameters?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Examples => Document.Components?.Examples?.ContainsKey(name) ?? false,
            OpenApiComponentKind.RequestBodies => Document.Components?.RequestBodies?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Headers => Document.Components?.Headers?.ContainsKey(name) ?? false,
            OpenApiComponentKind.SecuritySchemes => Document.Components?.SecuritySchemes?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Links => Document.Components?.Links?.ContainsKey(name) ?? false,
            OpenApiComponentKind.Callbacks => Document.Components?.Callbacks?.ContainsKey(name) ?? false,
            OpenApiComponentKind.PathItems => Document.Components?.PathItems?.ContainsKey(name) ?? false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// Tries to get an element by name from the specified component kind.
    /// </summary>
    /// <param name="name">The name of the element to retrieve.</param>
    /// <param name="kind">The kind of OpenAPI component to retrieve from.</param>
    /// <returns>The element if found; otherwise, null.</returns>
    public object? TryGetElementByName(string name, OpenApiComponentKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        return kind switch
        {
            OpenApiComponentKind.Schemas => Document.Components?.Schemas != null && Document.Components.Schemas.TryGetValue(name, out var schema) ? schema : null,
            OpenApiComponentKind.Responses => Document.Components?.Responses != null && Document.Components.Responses.TryGetValue(name, out var response) ? response : null,
            OpenApiComponentKind.Parameters => Document.Components?.Parameters != null && Document.Components.Parameters.TryGetValue(name, out var parameter) ? parameter : null,
            OpenApiComponentKind.Examples => Document.Components?.Examples != null && Document.Components.Examples.TryGetValue(name, out var example) ? example : null,
            OpenApiComponentKind.RequestBodies => Document.Components?.RequestBodies != null && Document.Components.RequestBodies.TryGetValue(name, out var requestBody) ? requestBody : null,
            OpenApiComponentKind.Headers => Document.Components?.Headers != null && Document.Components.Headers.TryGetValue(name, out var header) ? header : null,
            OpenApiComponentKind.SecuritySchemes => Document.Components?.SecuritySchemes != null && Document.Components.SecuritySchemes.TryGetValue(name, out var securityScheme) ? securityScheme : null,
            OpenApiComponentKind.Links => Document.Components?.Links != null && Document.Components.Links.TryGetValue(name, out var link) ? link : null,
            OpenApiComponentKind.Callbacks => Document.Components?.Callbacks != null && Document.Components.Callbacks.TryGetValue(name, out var callback) ? callback : null,
            OpenApiComponentKind.PathItems => Document.Components?.PathItems != null && Document.Components.PathItems.TryGetValue(name, out var pathItem) ? pathItem : null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// Tries to retrieve an OpenAPI component by name and kind.
    /// </summary>
    /// <typeparam name="T">The expected OpenAPI component type.</typeparam>
    /// <param name="name">The component name.</param>
    /// <param name="kind">The OpenAPI component kind.</param>
    /// <param name="value">When this method returns <c>true</c>, contains the component.</param>
    /// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
    public bool TryGetComponent<T>(
     string name,
     OpenApiComponentKind kind,
     out T? value)
     where T : class => TryGetFromComponents(Document.Components, name, kind, out value);

    /// <summary>
    /// Tries to retrieve an inline OpenAPI component by name and kind.
    /// </summary>
    /// <typeparam name="T"> The expected OpenAPI component type.</typeparam>
    /// <param name="name">The component name.</param>
    /// <param name="kind">The OpenAPI component kind.</param>
    /// <param name="value">When this method returns <c>true</c>, contains the component.</param>
    /// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
    public bool TryGetInline<T>(
        string name,
        OpenApiComponentKind kind,
        out T? value)
        where T : class => TryGetFromComponents(InlineComponents, name, kind, out value);

    /// <summary>
    /// Tries to retrieve an OpenAPI component from the specified components object.
    /// </summary>
    /// <typeparam name="T"> The expected OpenAPI component type.</typeparam>
    /// <param name="components">The OpenAPI components object.</param>
    /// <param name="name">The component name.</param>
    /// <param name="kind">The OpenAPI component kind.</param>
    /// <param name="value">When this method returns <c>true</c>, contains the component.</param>
    /// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static bool TryGetFromComponents<T>(
         OpenApiComponents? components,
         string name,
         OpenApiComponentKind kind,
         out T? value)
         where T : class
    {
        ArgumentNullException.ThrowIfNull(name);

        value = null;

        var c = components;
        if (c is null)
        {
            return false;
        }

        switch (kind)
        {
            case OpenApiComponentKind.Schemas:
                if (typeof(T) != typeof(OpenApiSchema))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Schemas is not null && c.Schemas.TryGetValue(name, out var schema))
                {
                    value = (T)(object)schema;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Responses:
                if (typeof(T) != typeof(OpenApiResponse))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Responses is not null && c.Responses.TryGetValue(name, out var response))
                {
                    value = (T)(object)response;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Parameters:
                if (typeof(T) != typeof(OpenApiParameter))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Parameters is not null && c.Parameters.TryGetValue(name, out var parameter))
                {
                    value = (T)(object)parameter;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Examples:
                if (typeof(T) != typeof(OpenApiExample))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Examples is not null && c.Examples.TryGetValue(name, out var example))
                {
                    value = (T)(object)example;
                    return true;
                }
                return false;

            case OpenApiComponentKind.RequestBodies:
                if (typeof(T) != typeof(OpenApiRequestBody))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.RequestBodies is not null && c.RequestBodies.TryGetValue(name, out var requestBody))
                {
                    value = (T)(object)requestBody;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Headers:
                if (typeof(T) != typeof(OpenApiHeader))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Headers is not null && c.Headers.TryGetValue(name, out var header))
                {
                    value = (T)(object)header;
                    return true;
                }
                return false;

            case OpenApiComponentKind.SecuritySchemes:
                if (typeof(T) != typeof(OpenApiSecurityScheme))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.SecuritySchemes is not null && c.SecuritySchemes.TryGetValue(name, out var scheme))
                {
                    value = (T)(object)scheme;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Links:
                if (typeof(T) != typeof(OpenApiLink))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Links is not null && c.Links.TryGetValue(name, out var link))
                {
                    value = (T)(object)link;
                    return true;
                }
                return false;

            case OpenApiComponentKind.Callbacks:
                if (typeof(T) != typeof(OpenApiCallback))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.Callbacks is not null && c.Callbacks.TryGetValue(name, out var callback))
                {
                    value = (T)(object)callback;
                    return true;
                }
                return false;

            case OpenApiComponentKind.PathItems:
                if (typeof(T) != typeof(OpenApiPathItem))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.PathItems is not null && c.PathItems.TryGetValue(name, out var pathItem))
                {
                    value = (T)(object)pathItem;
                    return true;
                }
                return false;

            case OpenApiComponentKind.MediaTypes:
                if (typeof(T) != typeof(OpenApiMediaType))
                {
                    ThrowTypeMismatch<T>(kind);
                }

                if (c.MediaTypes is not null && c.MediaTypes.TryGetValue(name, out var mediaType))
                {
                    value = (T)(object)mediaType;
                    return true;
                }
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }



    /// <summary>
    /// Tries to get a value from a dictionary.
    /// </summary>
    /// <typeparam name="T"> The expected OpenAPI component type.</typeparam>
    /// <param name="dict"> The dictionary to search.</param>
    /// <param name="name"> The key to look for in the dictionary.</param>
    /// <param name="value"> The value associated with the specified key, if found; otherwise, null.</param>
    /// <returns>True if the key was found; otherwise, false.</returns>
    private static bool TryGet<T>(
    IDictionary<string, T>? dict,
    string name,
    out T? value)
    where T : class
    {
        if (dict is not null && dict.TryGetValue(name, out var v))
        {
            value = v;
            return true;
        }

        value = null;
        return false;
    }

    [DoesNotReturn]
    private static void ThrowTypeMismatch<T>(OpenApiComponentKind kind)
    {
        throw new InvalidOperationException(
            $"Component kind '{kind}' does not match requested type '{typeof(T).Name}'.");
    }


}
