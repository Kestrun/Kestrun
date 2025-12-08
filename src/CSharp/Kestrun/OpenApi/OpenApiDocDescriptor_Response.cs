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
        string? defaultDescription = null;
        string? joinClassName = null;
        // Ensure Responses dictionary exists
        Document.Components!.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);

        // Scan properties for response-related attributes
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            var response = new OpenApiResponse();
            var classAttrs = t.GetCustomAttributes(inherit: false).
                                Where(a => a.GetType().Name is
                                nameof(OpenApiResponseComponent))
                                .Cast<object>()
                                .ToArray();
            if (classAttrs.Length > 0)
            {
                if (classAttrs.Length > 1)
                {
                    throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiResponseComponent] attributes. Only one is allowed per class.");
                }
                // Apply any class-level [OpenApiResponseComponent] attributes first
                if (classAttrs[0] is OpenApiResponseComponent classRespAttr)
                {
                    if (!string.IsNullOrEmpty(classRespAttr.Description))
                    {
                        defaultDescription = classRespAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classRespAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classRespAttr.JoinClassName;
                    }
                }
            }
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name is
                         (nameof(OpenApiResponseAttribute)) or
                         (nameof(OpenApiLinkRefAttribute)) or
                         (nameof(OpenApiHeaderRefAttribute)) or
                         (nameof(OpenApiExampleRefAttribute)))
                         .Cast<object>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }
            var hasResponseDef = false;
            var customName = string.Empty;
            // Support multiple attributes per property
            foreach (var a in attrs)
            {
                if (a is OpenApiResponseAttribute oaRa)
                {
                    if (!string.IsNullOrWhiteSpace(oaRa.Key))
                    {
                        customName = oaRa.Key;
                    }
                }
                var schema = GetAttributeValue(p);
                if (CreateResponseFromAttribute(a, response, schema))
                {
                    hasResponseDef = true;
                }
            }
            if (!hasResponseDef)
            {
                continue;
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (response.Description is null && defaultDescription is not null)
            {
                response.Description = defaultDescription;
            }
            // Apply default description if none set
            Document.Components!.Responses![key] = response;
            // Skip inferring schema/content for object-typed properties
            if (p.PropertyType.Name == "Object") { continue; }
        }
    }

    /// <summary>
    /// Gets the OpenAPI schema for a property based on its attributes and type.
    /// </summary>
    /// <param name="p"> The property info to get the schema for.</param>
    /// <returns> The OpenAPI schema for the property.</returns>
    private IOpenApiSchema GetAttributeValue(PropertyInfo p)
    {
        var pt = p.PropertyType;
        IOpenApiSchema iSchema;
        var allowNull = false;
        var underlying = Nullable.GetUnderlyingType(pt);
        if (underlying != null)
        {
            allowNull = true;
            pt = underlying;
        }

        if (pt.IsEnum)
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
            iSchema = schema;
        }
        else if (pt.IsArray)
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
            iSchema = schema;
        }
        else if (!IsPrimitiveLike(pt))
        {
            EnsureSchemaComponent(pt);

            iSchema = new OpenApiSchemaReference(pt.Name);
        }
        else
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
                iSchema = schema;
            }
            else
            {
                iSchema = sc;
            }
        }
        return iSchema;
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
        if (!string.IsNullOrEmpty(resp.Description))
        {
            response.Description = resp.Description;
        }
        if (resp.Schema is null && resp.SchemaRef is null && schema is not null)
        {
            // If no schema or schemaRef defined in attribute, but we have a schema from property, use it
            resp.SchemaRef = schema is OpenApiSchemaReference schRef ? schRef.Reference.Id : null;
        }

        // 1) Type-based schema (new behavior)
        if (resp.Schema is not null)
        {
            schema = InferPrimitiveSchema(resp.Schema, inline: resp.Inline);
        }

        // 2) Component reference (existing behavior)
        else if (resp.SchemaRef is not null)
        {
            schema = resp.Inline
                ? CloneSchemaOrThrow(resp.SchemaRef)
                : new OpenApiSchemaReference(resp.SchemaRef);
        }

        // If we have a schema, apply it to all declared content types
        if (schema is not null && resp.ContentType is { Length: > 0 })
        {
            foreach (var ct in resp.ContentType)
            {
                var media = GetOrAddMediaType(response, ct);
                media.Schema = schema; // or schema.Clone() if you need per-media isolation
            }
        }

        return true;
    }

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
