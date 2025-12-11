using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds response components from the specified type.
    /// </summary>
    /// <param name="t">The type to build responses for.</param>
    private void BuildResponses(Type t)
    {
        // Ensure Responses dictionary exists
        Document.Components!.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);

        var (defaultDescription, joinClassName) = GetClassLevelResponseMetadata(t);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            ProcessPropertyForResponse(p, defaultDescription, joinClassName);
        }
    }

    private static (string? Description, string? JoinClassName) GetClassLevelResponseMetadata(Type t)
    {
        string? description = null;
        string? joinClassName = null;

        var classAttrs = t.GetCustomAttributes(inherit: false)
            .Where(a => a.GetType().Name == nameof(OpenApiResponseComponent))
            .Cast<object>()
            .ToArray();

        if (classAttrs.Length > 1)
        {
            throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiResponseComponent] attributes. Only one is allowed per class.");
        }

        if (classAttrs.Length == 1 && classAttrs[0] is OpenApiResponseComponent attr)
        {
            if (!string.IsNullOrEmpty(attr.Description))
            {
                description = attr.Description;
            }
            if (!string.IsNullOrEmpty(attr.JoinClassName))
            {
                joinClassName = t.FullName + attr.JoinClassName;
            }
        }

        return (description, joinClassName);
    }

    private void ProcessPropertyForResponse(PropertyInfo p, string? defaultDescription, string? joinClassName)
    {
        var attrs = GetPropertyResponseAttributes(p);
        if (attrs.Length == 0)
        {
            return;
        }

        var response = new OpenApiResponse();
        var (hasResponseDef, customName) = ApplyPropertyAttributesToResponse(p, attrs, response);

        if (hasResponseDef)
        {
            RegisterResponse(response, p, customName, defaultDescription, joinClassName);
        }
    }

    private static object[] GetPropertyResponseAttributes(PropertyInfo p)
    {
        return [.. p.GetCustomAttributes(inherit: false)
             .Where(a => a.GetType().Name is
                 nameof(OpenApiResponseAttribute) or
                 nameof(OpenApiLinkRefAttribute) or
                 nameof(OpenApiHeaderRefAttribute) or
                 nameof(OpenApiExampleRefAttribute)
             )
             .Cast<object>()];
    }

    private (bool HasResponseDef, string CustomName) ApplyPropertyAttributesToResponse(PropertyInfo p, object[] attrs, OpenApiResponse response)
    {
        var hasResponseDef = false;
        var customName = string.Empty;

        foreach (var a in attrs)
        {
            if (a is OpenApiResponseAttribute oaRa && !string.IsNullOrWhiteSpace(oaRa.Key))
            {
                customName = oaRa.Key;
            }

            var schema = GetAttributeValue(p);
            if (CreateResponseFromAttribute(a, response, schema))
            {
                hasResponseDef = true;
            }
        }
        return (hasResponseDef, customName);
    }

    private void RegisterResponse(OpenApiResponse response, PropertyInfo p, string customName, string? defaultDescription, string? joinClassName)
    {
        var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName;
        var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;

        if (response.Description is null && defaultDescription is not null)
        {
            response.Description = defaultDescription;
        }

        Document.Components!.Responses![key] = response;
    }

    /// <summary>
    /// Gets the OpenAPI schema for a property based on its attributes and type.
    /// </summary>
    /// <param name="p"> The property info to get the schema for.</param>
    /// <returns> The OpenAPI schema for the property.</returns>
    private IOpenApiSchema GetAttributeValue(PropertyInfo p)
    {
        var pt = p.PropertyType;
        var allowNull = false;
        var underlying = Nullable.GetUnderlyingType(pt);
        if (underlying != null)
        {
            allowNull = true;
            pt = underlying;
        }
        // enum type
        if (pt.IsEnum)
        {
            return GetEnumSchema(p, pt, allowNull);
        }
        // array type
        if (pt.IsArray)
        {
            return GetArraySchema(p, pt, allowNull);
        }
        // complex type
        if (!IsPrimitiveLike(pt))
        {
            return GetComplexSchema(pt);
        }
        // primitive type
        return GetPrimitiveSchema(p, pt, allowNull);
    }

    /// <summary>
    /// Creates an OpenAPI schema for an enum property.
    /// </summary>
    /// <param name="p"> The property info.</param>
    /// <param name="pt"> The property type.</param>
    /// <param name="allowNull"> Indicates if null is allowed.</param>
    /// <returns> The OpenAPI schema.</returns>
    private static IOpenApiSchema GetEnumSchema(PropertyInfo p, Type pt, bool allowNull)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
        };
        var propAttrs = p.GetCustomAttributes<OpenApiPropertyAttribute>(inherit: false).ToArray();
        var a = MergeSchemaAttributes(propAttrs);
        ApplySchemaAttr(a, schema);
        PowerShellAttributes.ApplyPowerShellAttributes(p, schema);
        if (allowNull)
        {
            schema.Type |= JsonSchemaType.Null;
        }
        return schema;
    }

    private IOpenApiSchema GetArraySchema(PropertyInfo p, Type pt, bool allowNull)
    {
        var item = pt.GetElementType()!;
        IOpenApiSchema itemSchema;

        if (!IsPrimitiveLike(item) && !item.IsEnum)
        {
            // then reference it
            itemSchema = new OpenApiSchemaReference(item.Name);
        }
        else
        {
            itemSchema = InferPrimitiveSchema(item);
        }
        var schema = new OpenApiSchema
        {
            // then build the array schema
            Type = JsonSchemaType.Array,
            Items = itemSchema
        };
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), schema);
        PowerShellAttributes.ApplyPowerShellAttributes(p, schema);
        if (allowNull)
        {
            schema.Type |= JsonSchemaType.Null;
        }
        return schema;
    }

    private IOpenApiSchema GetComplexSchema(Type pt)
    {
        EnsureSchemaComponent(pt);
        return new OpenApiSchemaReference(pt.Name);
    }

    private IOpenApiSchema GetPrimitiveSchema(PropertyInfo p, Type pt, bool allowNull)
    {
        var sc = InferPrimitiveSchema(pt);
        if (sc is OpenApiSchema schema)
        {
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), schema);
            PowerShellAttributes.ApplyPowerShellAttributes(p, schema);
            if (allowNull)
            {
                schema.Type |= JsonSchemaType.Null;
            }
            return schema;
        }
        return sc;
    }

    /// <summary>
    /// Gets the name override from an attribute, if present.
    /// </summary>
    /// <param name="attr">The attribute to inspect.</param>
    /// <returns>The name override, if present; otherwise, null.</returns>
    private static string? GetKeyOverride(object attr)
    {
        var t = attr.GetType();
        return t.GetProperty("Key")?.GetValue(attr) as string;
    }

    /// <summary>
    /// Creates or modifies an OpenApiResponse based on the provided attribute.
    /// </summary>
    /// <param name="attr"> The attribute to apply.</param>
    /// <param name="response"> The OpenApiResponse to modify.</param>
    /// <param name="iSchema"> An optional schema to apply.</param>
    /// <returns>True if the response was modified; otherwise, false.</returns>
    private bool CreateResponseFromAttribute(object attr, OpenApiResponse response, IOpenApiSchema? iSchema = null)
    {
        ArgumentNullException.ThrowIfNull(attr);
        ArgumentNullException.ThrowIfNull(response);

        return attr switch
        {
            OpenApiResponseAttribute resp => ApplyResponseAttribute(resp, response, iSchema),
            OpenApiHeaderRefAttribute href => ApplyHeaderRefAttribute(href, response),
            OpenApiLinkRefAttribute lref => ApplyLinkRefAttribute(lref, response),
            OpenApiExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, response),
            _ => false
        };
    }
    // --- local helpers -------------------------------------------------------

    /// <summary>
    /// Applies an OpenApiResponseAttribute to an OpenApiResponse.
    /// </summary>
    /// <param name="resp">The OpenApiResponseAttribute to apply.</param>
    /// <param name="response">The OpenApiResponse to modify.</param>
    /// <param name="schema">An optional schema to apply.</param>
    /// <returns>True if the response was modified; otherwise, false.</returns>
    private bool ApplyResponseAttribute(OpenApiResponseAttribute resp, OpenApiResponse response, IOpenApiSchema? schema)
    {
        ApplyDescription(resp, response);
        schema = ResolveResponseSchema(resp, schema);
        ApplySchemaToContentTypes(resp, response, schema);
        return true;
    }

    private static void ApplyDescription(OpenApiResponseAttribute resp, OpenApiResponse response)
    {
        if (!string.IsNullOrEmpty(resp.Description))
        {
            response.Description = resp.Description;
        }
    }

    private IOpenApiSchema? ResolveResponseSchema(OpenApiResponseAttribute resp, IOpenApiSchema? propertySchema)
    {
        // 1) Type-based schema
        if (resp.Schema is not null)
        {
            return InferPrimitiveSchema(resp.Schema, inline: resp.Inline);
        }

        // 2) Explicit Component reference
        if (resp.SchemaRef is not null)
        {
            return ResolveSchemaRef(resp.SchemaRef, resp.Inline);
        }

        // 3) Fallback to property schema reference if available
        if (propertySchema is OpenApiSchemaReference refSchema && refSchema.Reference.Id is not null)
        {
            return ResolveSchemaRef(refSchema.Reference.Id, resp.Inline);
        }

        // 4) Fallback to existing property schema (primitive/concrete)
        return propertySchema;
    }

    private IOpenApiSchema ResolveSchemaRef(string refId, bool inline)
    {
        return inline
            ? CloneSchemaOrThrow(refId)
            : new OpenApiSchemaReference(refId);
    }

    private void ApplySchemaToContentTypes(OpenApiResponseAttribute resp, OpenApiResponse response, IOpenApiSchema? schema)
    {
        if (schema is not null && resp.ContentType is { Length: > 0 })
        {
            foreach (var ct in resp.ContentType)
            {
                var media = GetOrAddMediaType(response, ct);
                media.Schema = schema;
            }
        }
    }

    /// <summary>
    /// Applies a header reference attribute to an OpenAPI response.
    /// </summary>
    /// <param name="href">The header reference attribute.</param>
    /// <param name="response">The OpenAPI response to modify.</param>
    /// <returns>True if the header reference was applied; otherwise, false.</returns>
    private static bool ApplyHeaderRefAttribute(OpenApiHeaderRefAttribute href, OpenApiResponse response)
    {
        (response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))[href.Key] = new OpenApiHeaderReference(href.ReferenceId);
        return true;
    }

    private static bool ApplyLinkRefAttribute(OpenApiLinkRefAttribute lref, OpenApiResponse response)
    {
        (response.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal))[lref.Key] = new OpenApiLinkReference(lref.ReferenceId);
        return true;
    }

    private bool ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiResponse response)
    {
        var targets = exRef.ContentType is null
            ? (IEnumerable<string>)(response.Content?.Keys ?? Array.Empty<string>())
            : [exRef.ContentType];

        if (!targets.Any())
        {
            targets = ["application/json"];
        }

        foreach (var ct in targets)
        {
            var media = GetOrAddMediaType(response, ct);
            media.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            if (exRef.Inline)
            {
                if (Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(exRef.ReferenceId, out var value))
                {
                    throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it was not found in components.");
                }
                if (value is not OpenApiExample example)
                {
                    throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it is not an OpenApiExample.");
                }
                media.Examples[exRef.Key] = example.Clone();
            }
            else
            {
                media.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
            }
        }
        return true;
    }

    private OpenApiMediaType GetOrAddMediaType(OpenApiResponse resp, string contentType)
    {
        resp.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
        if (!resp.Content.TryGetValue(contentType, out var media))
        {
            media = resp.Content[contentType] = new OpenApiMediaType();
        }

        return media;
    }

    private OpenApiSchema CloneSchemaOrThrow(string refId)
    {
        if (Document.Components?.Schemas is { } schemas &&
            schemas.TryGetValue(refId, out var schema))
        {
            // your existing clone semantics
            return (OpenApiSchema)schema.Clone();
        }

        throw new InvalidOperationException(
            $"Schema reference '{refId}' cannot be embedded because it was not found in components.");
    }
}
