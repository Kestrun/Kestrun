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
        InlineComponents.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        AddComponent(InlineComponents.Examples, name,
                    example, ifExists,
                    OpenApiComponentKind.Examples);
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
        InlineComponents.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        AddComponent(InlineComponents.Links, name,
                           link, ifExists,
                           OpenApiComponentKind.Links);
    }

    /// <summary>
    /// Adds a component to the inline components of the OpenAPI document.
    /// </summary>
    /// <typeparam name="T">The type of the component to add. </typeparam>
    /// <param name="components">The dictionary of components to which the new component will be added.</param>
    /// <param name="name">The name of the component to add.</param>
    /// <param name="value">The component value to add.</param>
    /// <param name="ifExists">Specifies the behavior if a component with the same name already exists.</param>
    /// <param name="componentKind">The kind of component being added.</param>
    /// <exception cref="InvalidOperationException">Thrown if a component with the same name already exists and ifExists is set to Error.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the ifExists parameter has an invalid value.</exception>
    private static void AddComponent<T>(
        IDictionary<string, T> components,
        string name,
        T value,
        OpenApiComponentConflictResolution ifExists,
        OpenApiComponentKind componentKind)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        switch (ifExists)
        {
            case OpenApiComponentConflictResolution.Error:
                if (!components.TryAdd(name, value))
                {
                    var kind = componentKind.ToInlineLabel();
                    throw new InvalidOperationException(
                        $"A component {kind} named '{name}' already exists.");
                }
                return;

            case OpenApiComponentConflictResolution.Ignore:
                _ = components.TryAdd(name, value);
                return;

            case OpenApiComponentConflictResolution.Overwrite:
                components[name] = value;
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(ifExists), ifExists, null);
        }
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

        if (components is null)
        {
            return false;
        }

        ValidateComponentType<T>(kind);

        // NOTE: We intentionally avoid trying to up-cast IDictionary<string, TSpecific>
        // to IDictionary<string, object> because generic dictionaries are invariant.
        return kind switch
        {
            OpenApiComponentKind.Schemas => TryGetAndCast(components.Schemas, name, out value),
            OpenApiComponentKind.Responses => TryGetAndCast(components.Responses, name, out value),
            OpenApiComponentKind.Parameters => TryGetAndCast(components.Parameters, name, out value),
            OpenApiComponentKind.Examples => TryGetAndCast(components.Examples, name, out value),
            OpenApiComponentKind.RequestBodies => TryGetAndCast(components.RequestBodies, name, out value),
            OpenApiComponentKind.Headers => TryGetAndCast(components.Headers, name, out value),
            OpenApiComponentKind.SecuritySchemes => TryGetAndCast(components.SecuritySchemes, name, out value),
            OpenApiComponentKind.Links => TryGetAndCast(components.Links, name, out value),
            OpenApiComponentKind.Callbacks => TryGetAndCast(components.Callbacks, name, out value),
            OpenApiComponentKind.PathItems => TryGetAndCast(components.PathItems, name, out value),
            OpenApiComponentKind.MediaTypes => TryGetAndCast(components.MediaTypes, name, out value),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        static bool TryGetAndCast<TSpecific>(
            IDictionary<string, TSpecific>? dict,
            string componentName,
            out T? component)
            where TSpecific : class
        {
            if (TryGet(dict, componentName, out var specific) && specific is not null)
            {
                component = (T)(object)specific;
                return true;
            }

            component = null;
            return false;
        }
    }

    /// <summary>
    /// Validates that the specified type T matches the expected type for the given OpenApiComponentKind.
    /// </summary>
    /// <typeparam name="T">The expected OpenAPI component type.</typeparam>
    /// <param name="kind">The OpenAPI component kind.</param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the specified kind is not recognized.</exception>
    private static void ValidateComponentType<T>(OpenApiComponentKind kind) where T : class
    {
        var expectedType = kind switch
        {
            OpenApiComponentKind.Schemas => typeof(OpenApiSchema),
            OpenApiComponentKind.Responses => typeof(OpenApiResponse),
            OpenApiComponentKind.Parameters => typeof(OpenApiParameter),
            OpenApiComponentKind.Examples => typeof(OpenApiExample),
            OpenApiComponentKind.RequestBodies => typeof(OpenApiRequestBody),
            OpenApiComponentKind.Headers => typeof(OpenApiHeader),
            OpenApiComponentKind.SecuritySchemes => typeof(OpenApiSecurityScheme),
            OpenApiComponentKind.Links => typeof(OpenApiLink),
            OpenApiComponentKind.Callbacks => typeof(OpenApiCallback),
            OpenApiComponentKind.PathItems => typeof(OpenApiPathItem),
            OpenApiComponentKind.MediaTypes => typeof(OpenApiMediaType),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        if (typeof(T) != expectedType)
        {
            ThrowTypeMismatch<T>(kind);
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
