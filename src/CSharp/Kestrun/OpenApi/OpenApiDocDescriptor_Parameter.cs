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
    /// Builds OpenAPI parameters from a given type's properties.
    /// </summary>
    /// <param name="t">The type to build parameters from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the type has multiple [OpenApiResponseComponent] attributes.</exception>
    private void BuildParameters(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        Document.Components!.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            var parameter = new OpenApiParameter();
            var classAttrs = t.GetCustomAttributes(inherit: false).
                            Where(a => a.GetType().Name is
                            nameof(OpenApiParameterComponent))
                            .Cast<object>()
                            .ToArray();
            if (classAttrs.Length > 0)
            {
                if (classAttrs.Length > 1)
                {
                    throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiResponseComponent] attributes. Only one is allowed per class.");
                }
                // Apply any class-level [OpenApiResponseComponent] attributes first
                if (classAttrs[0] is OpenApiParameterComponent classRespAttr)
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
                         (nameof(OpenApiParameterAttribute)) or
                         (nameof(OpenApiPropertyAttribute)) or
                         (nameof(OpenApiExampleRefAttribute))
                         )
                         .Cast<KestrunAnnotation>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }
            var hasResponseDef = false;
            var customName = string.Empty;
            foreach (var a in attrs)
            {
                if (a is OpenApiParameterAttribute oaRa)
                {
                    if (!string.IsNullOrWhiteSpace(oaRa.Key))
                    {
                        customName = oaRa.Key;
                    }
                }
                if (CreateParameterFromAttribute(a, parameter))
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
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                parameter.Name = tname;
            }
            if (parameter.Description is null && defaultDescription is not null)
            {
                parameter.Description = defaultDescription;
            }
            Document.Components.Parameters[key] = parameter;

            var schemaAttr = (OpenApiPropertyAttribute?)p.GetCustomAttributes(inherit: false)
                              .Where(a => a.GetType().Name == "OpenApiPropertyAttribute")
                              .Cast<object>()
                              .LastOrDefault();
            // Build and assign the parameter schema
            parameter.Schema = CreatePropertySchema(schemaAttr, t, p);
        }
    }
    //todo: unify with BuildPropertySchema with BuildSchemaForType
    private IOpenApiSchema CreatePropertySchema(OpenApiPropertyAttribute? schemaAttr, Type t, PropertyInfo p)
    {
        IOpenApiSchema paramSchema;

        var pt = p.PropertyType;
        var allowNull = false;
        var underlying = Nullable.GetUnderlyingType(pt);
        if (underlying != null)
        {
            allowNull = true;
            pt = underlying;
        }
        // ENUM → string + enum list
        if (pt.IsEnum)
        {
            var s = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
            };
            ApplySchemaAttr(schemaAttr, s);
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
            paramSchema = s;
        }
        // ARRAY → array with item schema
        else if (pt.IsArray)
        {
            var elem = pt.GetElementType()!;
            IOpenApiSchema itemSchema;
            if (!IsPrimitiveLike(elem) && !elem.IsEnum)
            {
                // ensure a component schema exists for the complex element and $ref it
                EnsureSchemaComponent(elem);
                itemSchema = new OpenApiSchemaReference(elem.Name);
            }
            else
            {
                itemSchema = elem.IsEnum
                    ? new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = [.. elem.GetEnumNames().Select(n => (JsonNode)n)]
                    }
                    : InferPrimitiveSchema(elem);
            }

            var s = new OpenApiSchema { Type = JsonSchemaType.Array, Items = itemSchema };
            ApplySchemaAttr(schemaAttr, s);
            PowerShellAttributes.ApplyPowerShellAttributes(p, s);
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
            paramSchema = s;
        }
        // COMPLEX → ensure component + $ref
        else if (!IsPrimitiveLike(pt))
        {
            EnsureSchemaComponent(pt);
            var r = new OpenApiSchemaReference(pt.Name);
            ApplySchemaAttr(schemaAttr, r);
            paramSchema = r;
        }
        // PRIMITIVE
        else
        {
            var s = InferPrimitiveSchema(pt);
            ApplySchemaAttr(schemaAttr, s);
            PowerShellAttributes.ApplyPowerShellAttributes(p, s);
            // If no explicit default provided via schema attribute, try to pull default from property value
            if (s is OpenApiSchema sc && sc.Default is null)
            {
                try
                {
                    var inst = Activator.CreateInstance(t);
                    var val = p.GetValue(inst);
                    if (!IsIntrinsicDefault(val, p.PropertyType))
                    {
                        sc.Default = ToNode(val);
                    }
                }
                catch { }

                if (allowNull)
                {
                    sc.Type |= JsonSchemaType.Null;
                }
            }
            paramSchema = s;
        }
        return paramSchema;
    }

    private bool CreateParameterFromAttribute(KestrunAnnotation attr, OpenApiParameter parameter)
    {
        switch (attr)
        {
            case OpenApiParameterAttribute param:
                parameter.Description = param.Description;
                parameter.Name = string.IsNullOrEmpty(param.Name) ? param.Key : param.Name;
                parameter.Required = param.Required;
                parameter.Deprecated = param.Deprecated;
                parameter.AllowEmptyValue = param.AllowEmptyValue;
                if (param.Explode)
                {
                    parameter.Explode = param.Explode;
                }
                parameter.AllowReserved = param.AllowReserved;
                if (!string.IsNullOrEmpty(param.In))
                {
                    parameter.In = param.In.ToOpenApiParameterLocation();
                    if (parameter.In == ParameterLocation.Path)
                    {
                        parameter.Required = true; // path parameters must be required
                    }
                }

                if (param.Style is not null)
                {
                    parameter.Style = param.Style.ToParameterStyle();
                }
                if (param.Example is not null)
                {
                    parameter.Example = ToNode(param.Example);
                }
                break;

            case OpenApiExampleRefAttribute exRef:
                parameter.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
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
                    parameter.Examples[exRef.Key] = example.Clone();
                }
                else
                {
                    parameter.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
                }
                break;

            default:
                return false; // unrecognized attribute type
        }
        return true;
    }
    // ---- local helpers ----
}
