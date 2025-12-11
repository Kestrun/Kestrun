using System.Reflection;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds header components from the specified type.
    /// </summary>
    /// <param name="t">The type to build headers from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the type has multiple [OpenApiHeaderComponent] attributes.</exception>
    private void BuildHeaders(Type t)
    {
        var (defaultDescription, joinClassName) = GetHeaderComponentDefaults(t);
        Document.Components!.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var property in t.GetProperties(flags))
        {
            var attrs = GetHeaderAttributes(property);
            if (attrs.Length == 0)
            {
                continue;
            }

            var (header, customName) = BuildHeader(attrs);
            ApplyDefaultDescription(header, defaultDescription);

            var name = string.IsNullOrWhiteSpace(customName) ? property.Name : customName!;
            var key = BuildHeaderKey(joinClassName, name);
            Document.Components!.Headers![key] = header;
        }
    }

    /// <summary>
    /// Gets the default description and join class name from the OpenApiHeaderComponent attribute on the specified type.
    /// </summary>
    /// <param name="t">The type to inspect for the OpenApiHeaderComponent attribute.</param>
    /// <returns>A tuple containing the default description and join class name.</returns>
    private static (string? defaultDescription, string? joinClassName) GetHeaderComponentDefaults(Type t)
    {
        var componentAttr = t.GetCustomAttributes(inherit: false)
            .OfType<OpenApiHeaderComponent>()
            .FirstOrDefault();

        var defaultDescription = componentAttr?.Description;
        var joinClassName = componentAttr?.JoinClassName is { Length: > 0 } joinSuffix
            ? t.FullName + joinSuffix
            : null;

        return (defaultDescription, joinClassName);
    }

    /// <summary>
    /// Gets the header-related attributes from the specified property.
    /// </summary>
    /// <param name="property">The property to inspect for header-related attributes.</param>
    /// <returns>An array of header-related attributes found on the property.</returns>
    private static object[] GetHeaderAttributes(PropertyInfo property)
    {
        return
        [
            .. property
                .GetCustomAttributes(inherit: false)
                .Where(a => a is OpenApiHeaderAttribute or OpenApiExampleRefAttribute or OpenApiExampleAttribute)
        ];
    }
    /// <summary>
    /// Builds an OpenApiHeader from the specified attributes.
    /// </summary>
    /// <param name="attributes">An array of attributes to build the header from.</param>
    /// <returns></returns>
    private static (OpenApiHeader header, string? customName) BuildHeader(object[] attributes)
    {
        var header = new OpenApiHeader();
        string? customName = null;

        foreach (var attribute in attributes)
        {
            if (attribute is OpenApiHeaderAttribute headerAttr && !string.IsNullOrWhiteSpace(headerAttr.Key))
            {
                customName = headerAttr.Key;
            }

            _ = CreateHeaderFromAttribute(attribute, header);
        }

        return (header, customName);
    }

    /// <summary>
    /// Applies the default description to the OpenApiHeader if it does not already have a description.
    /// </summary>
    /// <param name="header">The OpenApiHeader to apply the default description to.</param>
    /// <param name="defaultDescription">The default description to apply if the header's description is null.</param>
    private static void ApplyDefaultDescription(OpenApiHeader header, string? defaultDescription)
    {
        if (header.Description is null && defaultDescription is not null)
        {
            header.Description = defaultDescription;
        }
    }

    /// <summary>
    /// Builds the header key using the join class name and the header name.
    /// </summary>
    /// <param name="joinClassName">The join class name to prepend to the header name.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The combined header key.</returns>
    private static string BuildHeaderKey(string? joinClassName, string name) =>
        joinClassName is not null ? $"{joinClassName}{name}" : name;

    /// <summary>
    /// Creates an OpenApiHeader from the specified supported attribute types.
    /// </summary>
    /// <param name="attr">Attribute instance.</param>
    /// <param name="header">Target header to populate.</param>
    /// <returns>True when the attribute type was recognized and applied; otherwise false.</returns>
    private static bool CreateHeaderFromAttribute(object attr, OpenApiHeader header)
    {
        return attr switch
        {
            OpenApiHeaderAttribute h => ApplyHeaderAttribute(h, header),
            OpenApiExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, header),
            OpenApiExampleAttribute ex => ApplyInlineExampleAttribute(ex, header),

            _ => false
        };
    }

    private static bool ApplyHeaderAttribute(OpenApiHeaderAttribute attribute, OpenApiHeader header)
    {
        header.Description = attribute.Description;
        header.Required = attribute.Required;
        header.Deprecated = attribute.Deprecated;
        header.AllowEmptyValue = attribute.AllowEmptyValue;
        header.Schema = string.IsNullOrWhiteSpace(attribute.SchemaRef)
            ? new OpenApiSchema { Type = JsonSchemaType.String }
            : new OpenApiSchemaReference(attribute.SchemaRef);
        header.Style = attribute.Style.ToOpenApi();
        header.AllowReserved = attribute.AllowReserved;
        header.Explode = attribute.Explode;
        if (attribute.Example is not null)
        {
            header.Example = ToNode(attribute.Example);
        }
        return true;
    }
}
