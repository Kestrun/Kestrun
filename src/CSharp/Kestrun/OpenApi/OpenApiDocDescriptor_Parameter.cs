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
        Document.Components!.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);

        var (defaultDescription, joinClassName) = GetClassLevelMetadata(t);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            ProcessPropertyForParameter(p, t, defaultDescription, joinClassName);
        }
    }

    /// <summary>
    /// Retrieves class-level OpenAPI metadata from the given type.
    /// </summary>
    /// <param name="t">The type to retrieve metadata from.</param>
    /// <returns>A tuple containing the description and join class name, if any.</returns>
    private static (string? Description, string? JoinClassName) GetClassLevelMetadata(Type t)
    {
        string? description = null;
        string? joinClassName = null;

        var classAttrs = t.GetCustomAttributes(inherit: false)
            .Where(a => a.GetType().Name == nameof(OpenApiParameterComponent))
            .Cast<object>()
            .ToArray();

        if (classAttrs.Length > 1)
        {
            throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiParameterComponent] attributes. Only one is allowed per class.");
        }

        if (classAttrs.Length == 1 && classAttrs[0] is OpenApiParameterComponent attr)
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

    /// <summary>
    /// Processes a property to create and register an OpenAPI parameter if applicable.
    /// </summary>
    /// <param name="p">The PropertyInfo representing the property.</param>
    /// <param name="t">The type that declares the property.</param>
    /// <param name="defaultDescription">A default description to apply if the parameter's description is not set.</param>
    /// <param name="joinClassName">An optional string to join with the class name for unique key generation.</param>
    private void ProcessPropertyForParameter(PropertyInfo p, Type t, string? defaultDescription, string? joinClassName)
    {
        var parameter = new OpenApiParameter();
        var attrs = GetParameterAttributes(p);

        if (attrs.Length == 0)
        {
            return;
        }

        var (hasResponseDef, customName) = ApplyParameterAttributes(parameter, attrs);

        if (hasResponseDef)
        {
            FinalizeAndRegisterParameter(parameter, p, t, customName, defaultDescription, joinClassName);
        }
    }

    /// <summary>
    /// Retrieves parameter-related attributes from a property.
    /// </summary>
    /// <param name="p">The PropertyInfo representing the property.</param>
    /// <returns>An array of KestrunAnnotation attributes related to parameters.</returns>
    private static KestrunAnnotation[] GetParameterAttributes(PropertyInfo p)
    {
        return
        [
            .. p.GetCustomAttributes(inherit: false)
             .Where(a => a.GetType().Name is
                 nameof(OpenApiParameterAttribute) or
                 nameof(OpenApiPropertyAttribute) or
                 nameof(OpenApiExampleRefAttribute)
             )
             .Cast<KestrunAnnotation>()
        ];
    }

    /// <summary>
    /// Applies parameter-related attributes to the given OpenApiParameter.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to apply attributes to.</param>
    /// <param name="attrs">An array of KestrunAnnotation attributes to apply.</param>
    /// <returns>A tuple indicating if a response definition was found and a custom name, if any.</returns>
    private (bool HasResponseDef, string CustomName) ApplyParameterAttributes(OpenApiParameter parameter, KestrunAnnotation[] attrs)
    {
        var hasResponseDef = false;
        var customName = string.Empty;

        foreach (var a in attrs)
        {
            if (a is OpenApiParameterAttribute oaRa && !string.IsNullOrWhiteSpace(oaRa.Key))
            {
                customName = oaRa.Key;
            }

            if (CreateParameterFromAttribute(a, parameter))
            {
                hasResponseDef = true;
            }
        }
        return (hasResponseDef, customName);
    }

    /// <summary>
    /// Finalizes and registers the OpenAPI parameter in the document components.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to finalize and register.</param>
    /// <param name="p">The PropertyInfo representing the property.</param>
    /// <param name="t">The type that declares the property.</param>
    /// <param name="customName">A custom name for the parameter, if specified.</param>
    /// <param name="defaultDescription">A default description to apply if the parameter's description is not set.</param>
    /// <param name="joinClassName">An optional string to join with the class name for unique key generation.</param>
    private void FinalizeAndRegisterParameter(OpenApiParameter parameter, PropertyInfo p, Type t, string customName, string? defaultDescription, string? joinClassName)
    {
        var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName;
        var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;

        if (string.IsNullOrWhiteSpace(parameter.Name))
        {
            parameter.Name = tname;
        }
        if (parameter.Description is null && defaultDescription is not null)
        {
            parameter.Description = defaultDescription;
        }

        Document.Components!.Parameters![key] = parameter;

        var schemaAttr = (OpenApiPropertyAttribute?)p.GetCustomAttributes(inherit: false)
                          .LastOrDefault(a => a.GetType().Name == nameof(OpenApiPropertyAttribute));

        parameter.Schema = CreatePropertySchema(schemaAttr, t, p);
    }

    /// <summary>
    /// Creates an OpenAPI schema for a property based on its type and any associated OpenApiPropertyAttribute.
    /// </summary>
    /// <param name="schemaAttr">The OpenApiPropertyAttribute associated with the property, if any.</param>
    /// <param name="t">The type that declares the property.</param>
    /// <param name="p">The PropertyInfo representing the property.</param>
    /// <returns>An IOpenApiSchema representing the property's schema.</returns>
    private IOpenApiSchema CreatePropertySchema(OpenApiPropertyAttribute? schemaAttr, Type t, PropertyInfo p)
    {
        var pt = p.PropertyType;
        var allowNull = false;
        var underlying = Nullable.GetUnderlyingType(pt);
        if (underlying != null)
        {
            allowNull = true;
            pt = underlying;
        }
        // enums first
        if (pt.IsEnum)
        {
            return CreateEnumSchema(pt, schemaAttr, allowNull);
        }
        // check for array after enum to handle enum arrays
        if (pt.IsArray)
        {
            return CreateArraySchema(pt, p, schemaAttr, allowNull);
        }
        // complex types
        if (!IsPrimitiveLike(pt))
        {
            return CreateComplexSchema(pt, schemaAttr);
        }
        // primitive types
        return CreatePrimitiveSchema(pt, t, p, schemaAttr, allowNull);
    }

    /// <summary>
    /// Creates an OpenAPI schema for an enum property.
    /// </summary>
    /// <param name="pt">The enum type of the property</param>
    /// <param name="schemaAttr">Optional OpenApiPropertyAttribute for the property</param>
    /// <param name="allowNull">Indicates if the property allows null values</param>
    /// <returns>An IOpenApiSchema representing the enum property</returns>
    private static IOpenApiSchema CreateEnumSchema(Type pt, OpenApiPropertyAttribute? schemaAttr, bool allowNull)
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
        return s;
    }

    /// <summary>
    /// Creates an OpenAPI schema for an array property.
    /// </summary>
    /// <param name="pt">The array type of the property</param>
    /// <param name="p">The PropertyInfo representing the property</param>
    /// <param name="schemaAttr">Optional OpenApiPropertyAttribute for the property</param>
    /// <param name="allowNull">Indicates if the property allows null values</param>
    /// <returns>An IOpenApiSchema representing the array property</returns>
    private IOpenApiSchema CreateArraySchema(Type pt, PropertyInfo p, OpenApiPropertyAttribute? schemaAttr, bool allowNull)
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
        return s;
    }

    /// <summary>
    /// Creates an OpenAPI schema for a complex property.
    /// </summary>
    /// <param name="pt">The complex type of the property</param>
    /// <param name="schemaAttr">Optional OpenApiPropertyAttribute for the property</param>
    /// <returns>An IOpenApiSchema representing the complex property</returns>
    private IOpenApiSchema CreateComplexSchema(Type pt, OpenApiPropertyAttribute? schemaAttr)
    {
        EnsureSchemaComponent(pt);
        var r = new OpenApiSchemaReference(pt.Name);
        ApplySchemaAttr(schemaAttr, r);
        return r;
    }

    /// <summary>
    /// Creates an OpenAPI schema for a primitive property.
    /// </summary>
    /// <param name="pt">The primitive type of the property</param>
    /// <param name="t">The containing type</param>
    /// <param name="p">The PropertyInfo representing the property</param>
    /// <param name="schemaAttr">Optional OpenApiPropertyAttribute for the property</param>
    /// <param name="allowNull">Indicates if the property allows null values</param>
    /// <returns>An IOpenApiSchema representing the primitive property</returns>
    private IOpenApiSchema CreatePrimitiveSchema(Type pt, Type t, PropertyInfo p, OpenApiPropertyAttribute? schemaAttr, bool allowNull)
    {
        var s = InferPrimitiveSchema(pt);
        ApplySchemaAttr(schemaAttr, s);
        PowerShellAttributes.ApplyPowerShellAttributes(p, s);
        // If no explicit default provided via schema attribute, try to pull default from property value
        if (s is OpenApiSchema sc && sc.Default is null)
        {
            TryApplyDefaultValue(t, p, sc);

            if (allowNull)
            {
                sc.Type |= JsonSchemaType.Null;
            }
        }
        return s;
    }

    /// <summary>
    /// Tries to apply the default value of a property to the given OpenApiSchema.
    /// </summary>
    /// <param name="t">Type containing the property</param>
    /// <param name="p">PropertyInfo of the property</param>
    /// <param name="sc">OpenApiSchema to apply the default value to</param>
    private static void TryApplyDefaultValue(Type t, PropertyInfo p, OpenApiSchema sc)
    {
        try
        {
            var inst = Activator.CreateInstance(t);
            if (inst != null)
            {
                var val = p.GetValue(inst);
                if (!IsIntrinsicDefault(val, p.PropertyType))
                {
                    sc.Default = ToNode(val);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Creates an OpenAPI parameter from a given attribute.
    /// </summary>
    /// <param name="attr">The attribute to create the parameter from</param>
    /// <param name="parameter">The OpenApiParameter object to populate</param>
    /// <returns>True if the parameter was created successfully, otherwise false</returns>
    /// <exception cref="InvalidOperationException">Thrown when an example reference cannot be embedded due to missing or invalid components.</exception>
    private bool CreateParameterFromAttribute(KestrunAnnotation attr, OpenApiParameter parameter)
    {
        switch (attr)
        {
            case OpenApiParameterAttribute param:
                ApplyParameterAttribute(param, parameter);
                break;

            case OpenApiExampleRefAttribute exRef:
                ApplyExampleRefAttribute(exRef, parameter);
                break;

            default:
                return false; // unrecognized attribute type
        }
        return true;
    }

    /// <summary>
    /// Applies an OpenApiParameterAttribute to an OpenApiParameter.
    /// </summary>
    /// <param name="param">The OpenApiParameterAttribute to apply</param>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    private static void ApplyParameterAttribute(OpenApiParameterAttribute param, OpenApiParameter parameter)
    {
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
    }

    /// <summary>
    /// Applies an example reference attribute to an OpenAPI parameter.
    /// </summary>
    /// <param name="exRef">The OpenApiExampleRefAttribute to apply</param>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    private void ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiParameter parameter)
    {
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
    }
}
