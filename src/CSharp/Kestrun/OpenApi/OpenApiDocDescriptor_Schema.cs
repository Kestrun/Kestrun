using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Kestrun.Runtime;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    #region Schemas
    private static OpenApiProperties? GetSchemaIdentity(Type t)
    {
        // Prefer OpenApiPropertyAttribute (it supports operation/property-level overrides),
        // but fall back to any OpenApiProperties-derived attribute (e.g., OpenApiSchemaComponent).
        var propAttrs = (OpenApiPropertyAttribute[])t.GetCustomAttributes(typeof(OpenApiPropertyAttribute), inherit: true);
        if (propAttrs.Length > 0)
        {
            return propAttrs[0];
        }

        // Note: OpenApiSchemaComponent is Inherited=false, so inherit:true won't climb.
        // We walk the base chain manually to allow schemas deriving from OpenApi* primitives
        // (or from a base schema component) to inherit the underlying identity.
        for (var current = t; current is not null && current != typeof(object); current = current.BaseType)
        {
            var schemaAttrs = current.GetCustomAttributes(inherit: false).OfType<OpenApiProperties>().ToArray();
            if (schemaAttrs.Length > 0)
            {
                return schemaAttrs[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Builds and returns the schema for a given type.
    /// </summary>
    /// <param name="t">Type to build schema for</param>
    /// <param name="built">Set of types already built to avoid recursion</param>
    /// <returns>OpenApiSchema representing the type</returns>
    private IOpenApiSchema BuildSchemaForType(Type t, HashSet<Type>? built = null)
    {
        built ??= [];
        OpenApiSchema schema;
        OpenApiSchema? schemaParent = null;
        if (PrimitiveSchemaMap.TryGetValue(t, out var getSchema))
        {
            schema = getSchema();
            // Apply type-level attributes
            ApplyTypeAttributes(t, schema);
            return schema;
        }
        else
        {

            // Handle custom base type derivations first
            if (t.BaseType is not null && t.BaseType != typeof(object))
            {
                var baseSchema = BuildBaseTypeSchema(t);
                if (baseSchema is not null)
                {
                    if ((baseSchema.AllOf is null && baseSchema.Type != JsonSchemaType.Array) || baseSchema is OpenApiSchemaReference)
                    {
                        if (baseSchema is OpenApiSchema oschema)
                        {
                            ApplyTypeAttributes(t, oschema);
                            return oschema;
                        }
                        return baseSchema;
                    }
                    else if (baseSchema is OpenApiSchema oschema && baseSchema.Type == JsonSchemaType.Array && baseSchema.Items is not null)
                    {
                        ApplyTypeAttributes(t, oschema);
                        if (baseSchema.Items is OpenApiSchema itemSchema)
                        {
                            ApplyTypeAttributes(t, oschema);
                            if (itemSchema.AllOf is null)
                            {
                                return oschema;
                            }
                            else
                            {
                                schema = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object,
                                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                                };
                                ProcessTypeProperties(t, schema, built);
                                itemSchema.AllOf.Add(schema);
                                return oschema;
                            }
                        }
                        else
                        {
                            return oschema;
                        }
                    }
                }
                schemaParent = baseSchema as OpenApiSchema;
            }
        }

        schema = new OpenApiSchema();
        var declaredPropsCount =
           t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.DeclaringType == t);
        if (declaredPropsCount > 0)
        {
            schema.Type = JsonSchemaType.Object;
            schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        }

        // Prevent infinite recursion
        if (built.Contains(t))
        {
            return schema;
        }

        _ = built.Add(t);

        // Apply type-level attributes
        ApplyTypeAttributes(t, schema);

        // Handle enum types
        if (t.IsEnum)
        {
            RegisterEnumSchema(t);
            return schema;
        }
        // Process properties with default value capture
        ProcessTypeProperties(t, schema, built);
        if (schemaParent is not null)
        {
            if (schemaParent.AllOf is not null)
            {
                //var baseRef = new OpenApiSchemaReference(t.BaseType!.Name);
                schemaParent.AllOf.Add(schema);
                schemaParent.Type = null; // Clear type when using allOf
                return schemaParent;
            }

            if (schemaParent.Type == JsonSchemaType.Array)
            {
                if (schemaParent.Items is not null &&
                            schemaParent.Items is OpenApiSchema &&
                              schemaParent.Items.AllOf is not null)
                {
                    schemaParent.Items.AllOf.Add(schema);
                }
                return schemaParent;
            }
        }
        return schema;
    }

    /// <summary>
    /// Builds schema for custom base type derivations.
    /// </summary>
    ///  <param name="t">Type to build schema for</param>
    /// <returns>OpenApiSchema representing the base type derivation, or null if not applicable</returns>
    private static IOpenApiSchema? BuildBaseTypeSchema(Type t)
    {
        if (PrimitiveSchemaMap.TryGetValue(t.BaseType!, out var value))
        {
            return value();
        }

        //   var primitivesAssembly = typeof(OpenApiString).Assembly;

        // Only treat built-in OpenApi* primitives (and their variants) as raw OpenApi types.
        // User-defined schema components (including array wrappers like EventDates : Date)
        // should fall through to BuildCustomBaseTypeSchema so we can emit $ref + array items.
        //  if (t.Assembly == primitivesAssembly && typeof(IOpenApiType).IsAssignableFrom(t))
        //  {
        //       return BuildOpenApiTypeSchema(t);
        //  }
        // Fallback to custom base type schema building
        return BuildCustomBaseTypeSchema(t);
    }

    /*
    /// <summary>
     /// Builds schema for types implementing IOpenApiType.
     /// </summary>
     private static OpenApiSchema? BuildOpenApiTypeSchema(Type t)
     {
         var attr = GetSchemaIdentity(t);
         return attr is not null
             ? new OpenApiSchema
             {
                 Type = attr.Type.ToJsonSchemaType(),
                 Format = attr.Format
             }
             : null;
     }*/

    /// <summary>
    /// Builds schema for types with custom base types.
    /// </summary>
    /// <param name="t">Type to build schema for</param>
    /// <returns>OpenApiSchema representing the custom base type derivation</returns>
    private static IOpenApiSchema BuildCustomBaseTypeSchema(Type t)
    {
        var attributes = t.CustomAttributes.ToArray();
        // Count declared properties
        var declaredPropsCount =
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Count(p => p.DeclaringType == t);

        // Check for the special case where the derived type only adds array semantics
        var hasArray =
            attributes.Length > 0 &&
            attributes[0].NamedArguments.Any(na =>
                na.MemberName == "Array" &&
                na.TypedValue.ArgumentType == typeof(bool) &&
                na.TypedValue.Value is bool b && b);
        // If so, we can represent this as a simple reference to the base type
        if (declaredPropsCount == 0 && attributes.Length == 1)
        {
            if (hasArray)
            {
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(t.BaseType!.Name)
                };
            }
            // If the derived type has AdditionalProperties, we can't use allOf
            return new OpenApiSchemaReference(t.BaseType!.Name);
        }
        // Otherwise, build an allOf schema referencing the base type
        var schema = new OpenApiSchema
        {
            AllOf = [new OpenApiSchemaReference(t.BaseType!.Name)]
        };
        // Apply array semantics if specified
        return hasArray
            ? new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = schema
            }
            : schema;
    }

    /// <summary>
    /// Registers an enum type schema in the document components.
    /// </summary>
    private void RegisterEnumSchema(Type enumType)
    {
        if (Document.Components?.Schemas is not null)
        {
            Document.Components.Schemas[enumType.Name] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. enumType.GetEnumNames().Select(n => (JsonNode)n)]
            };
        }
    }

    /// <summary>
    /// Applies type-level attributes to a schema.
    /// </summary>
    private static void ApplyTypeAttributes(Type t, OpenApiSchema schema)
    {
        foreach (var attr in t.GetCustomAttributes(true)
          .Where(a => a is OpenApiPropertyAttribute or OpenApiSchemaComponent))
        {
            ApplySchemaAttr(attr as OpenApiProperties, schema);

            if (attr is OpenApiSchemaComponent schemaAttribute && schemaAttribute.Examples is not null)
            {
                schema.Examples ??= [];
                var node = OpenApiJsonNodeFactory.ToNode(schemaAttribute.Examples);
                if (node is not null)
                {
                    schema.Examples.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Processes all properties of a type and builds their schemas.
    /// </summary>
    private void ProcessTypeProperties(Type t, OpenApiSchema schema, HashSet<Type> built)
    {
        var instance = TryCreateTypeInstance(t);

        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                      .Where(p => p.DeclaringType == t))
        {
            var propSchema = BuildPropertySchema(prop, built);
            CapturePropertyDefault(instance, prop, propSchema);

            if (prop.GetCustomAttribute<OpenApiAdditionalPropertiesAttribute>() is not null)
            {
                schema.AdditionalPropertiesAllowed = true;
                schema.AdditionalProperties = propSchema;
            }
            else
            {
                schema.Properties?.Add(prop.Name, propSchema);
            }
        }
    }

    /// <summary>
    /// Attempts to create an instance of a type to capture default values.
    /// </summary>
    private static object? TryCreateTypeInstance(Type t)
    {
        try
        {
            return Activator.CreateInstance(t);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Captures the default value of a property if not already set.
    /// </summary>
    private static void CapturePropertyDefault(object? instance, PropertyInfo prop, IOpenApiSchema propSchema)
    {
        if (instance is null || propSchema is not OpenApiSchema concrete || concrete.Default is not null)
        {
            return;
        }

        try
        {
            var value = prop.GetValue(instance);
            if (!IsIntrinsicDefault(value, prop.PropertyType))
            {
                concrete.Default = OpenApiJsonNodeFactory.ToNode(value);
            }
        }
        catch
        {
            // Ignore failures when capturing defaults
        }
    }

    /// <summary>
    /// Determines if a value is the intrinsic default for its declared type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="declaredType">The declared type of the value.</param>
    /// <returns>True if the value is the intrinsic default for its declared type; otherwise, false.</returns>
    private static bool IsIntrinsicDefault(object? value, Type declaredType)
    {
        if (value is null)
        {
            return true;
        }

        // Unwrap Nullable<T>
        var t = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        // Reference types: null is the only intrinsic default
        if (!t.IsValueType)
        {
            return false;
        }

        // Special-cases for common structs
        if (t == typeof(Guid))
        {
            return value.Equals(Guid.Empty);
        }

        if (t == typeof(TimeSpan))
        {
            return value.Equals(TimeSpan.Zero);
        }

        if (t == typeof(DateTime))
        {
            return value.Equals(default(DateTime));
        }

        if (t == typeof(DateTimeOffset))
        {
            return value.Equals(default(DateTimeOffset));
        }

        // Enums: 0 is intrinsic default
        if (t.IsEnum)
        {
            return Convert.ToInt64(value) == 0;
        }

        // Primitive/value types: compare to default(T)
        var def = Activator.CreateInstance(t);
        return value.Equals(def);
    }

    /// <summary>
    /// Makes an OpenApiSchema nullable if specified.
    /// </summary>
    /// <param name="schema">The OpenApiSchema to modify.</param>
    /// <param name="isNullable">Indicates whether the schema should be nullable.</param>
    /// <returns>The modified OpenApiSchema.</returns>
    private static OpenApiSchema MakeNullable(OpenApiSchema schema, bool isNullable)
    {
        if (isNullable)
        {
            schema.Type |= JsonSchemaType.Null;
        }
        return schema;
    }

    /// <summary>
    /// Infers a primitive OpenApiSchema from a .NET type.
    /// </summary>
    /// <param name="type">The .NET type to infer from.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    public IOpenApiSchema InferPrimitiveSchema(Type type, bool inline = false)
    {
        var nullable = false;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)
            && type.GetGenericArguments().Length == 1)
        {
            type = type.GetGenericArguments()[0];
            nullable = true;
        }
        // Direct type mappings
        if (PrimitiveSchemaMap.TryGetValue(type, out var schemaFactory))
        {
            return MakeNullable(schemaFactory(), nullable);
        }

        // Array type handling
        if (type.Name.EndsWith("[]"))
        {
            return InferArraySchema(type, inline);
        }

        // Special handling for PowerShell OpenAPI classes
        if (PowerShellOpenApiClassExporter.ValidClassNames.Contains(type.Name))
        {
            return InferPowerShellClassSchema(type, inline);
        }

        // Fallback
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    /// <summary>
    /// Infers an array OpenApiSchema from a .NET array type.
    /// </summary>
    /// <param name="type">The .NET array type to infer from.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private OpenApiSchema InferArraySchema(Type type, bool inline)
    {
        var typeName = type.Name[..^2];
        if (ComponentSchemasExists(typeName))
        {
            var items = inline ? GetSchema(typeName).Clone() : new OpenApiSchemaReference(typeName);
            return new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };
        }

        return new OpenApiSchema { Type = JsonSchemaType.Array, Items = InferPrimitiveSchema(type.GetElementType() ?? typeof(object)) };
    }

    /// <summary>
    /// Infers a PowerShell OpenAPI class schema.
    /// </summary>
    /// <param name="type">The .NET type representing the PowerShell OpenAPI class.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private IOpenApiSchema InferPowerShellClassSchema(Type type, bool inline)
    {
        var schema = GetSchema(type.Name);

        if (inline)
        {
            if (schema is OpenApiSchema concreteSchema)
            {
                return concreteSchema.Clone();
            }
        }
        else
        {
            if (schema is not null)
            {
                return new OpenApiSchemaReference(type.Name);
            }
        }

        Host.Logger.Warning("Schema for PowerShell OpenAPI class '{typeName}' not found. Defaulting to string schema.", type.Name);
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    /// <summary>
    /// Mapping of .NET primitive types to OpenAPI schema definitions.
    /// </summary>
    /// <remarks>
    /// This dictionary maps common .NET primitive types to their corresponding OpenAPI schema representations.
    /// Each entry consists of a .NET type as the key and a function that returns an OpenApiSchema as the value.
    /// </remarks>
    private static readonly Dictionary<Type, Func<OpenApiSchema>> PrimitiveSchemaMap = new()
    {
        [typeof(string)] = () => new OpenApiSchema { Type = JsonSchemaType.String },
        [typeof(bool)] = () => new OpenApiSchema { Type = JsonSchemaType.Boolean },
        [typeof(long)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(DateTime)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(DateTimeOffset)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(TimeSpan)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "duration" },
        [typeof(byte[])] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        [typeof(Uri)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
        [typeof(Guid)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        [typeof(object)] = () => new OpenApiSchema { Type = JsonSchemaType.Object },
        [typeof(void)] = () => new OpenApiSchema { Type = JsonSchemaType.Null },
        [typeof(char)] = () => new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 1, MinLength = 1 },
        [typeof(sbyte)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(byte)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(short)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(ushort)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(int)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(uint)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(long)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(ulong)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(float)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
        [typeof(double)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },
        [typeof(decimal)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" },
        [typeof(OpenApiString)] = () => new OpenApiSchema { Type = JsonSchemaType.String },
        [typeof(OpenApiUuid)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        [typeof(OpenApiDate)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
        [typeof(OpenApiDateTime)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(OpenApiEmail)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "email" },
        [typeof(OpenApiBinary)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        [typeof(OpenApiHostname)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "hostname" },
        [typeof(OpenApiIpv4)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "ipv4" },
        [typeof(OpenApiIpv6)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "ipv6" },
        [typeof(OpenApiUri)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
        [typeof(OpenApiUrl)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "url" },
        [typeof(OpenApiByte)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" },
        [typeof(OpenApiPassword)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "password" },
        [typeof(OpenApiRegex)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "regex" },
        [typeof(OpenApiJson)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "json" },
        [typeof(OpenApiXml)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "xml" },
        [typeof(OpenApiYaml)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "yaml" },

        [typeof(OpenApiInteger)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer },
        [typeof(OpenApiInt32)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(OpenApiInt64)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },

        [typeof(OpenApiNumber)] = () => new OpenApiSchema { Type = JsonSchemaType.Number },
        [typeof(OpenApiFloat)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
        [typeof(OpenApiDouble)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },

        [typeof(OpenApiBoolean)] = () => new OpenApiSchema { Type = JsonSchemaType.Boolean, Format = "boolean" },
    };

    /// <summary>
    /// Applies schema attributes to an OpenAPI schema.
    /// </summary>
    /// <param name="oaProperties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="ioaSchema">The OpenAPI schema to apply attributes to.</param>
    private static void ApplySchemaAttr(OpenApiProperties? oaProperties, IOpenApiSchema ioaSchema)
    {
        if (oaProperties is null)
        {
            return;
        }

        // Most models implement OpenApiSchema (concrete) OR OpenApiSchemaReference.
        // We set common metadata when possible (Description/Title apply only to concrete schema).
        if (ioaSchema is OpenApiSchema concreteSchema)
        {
            ApplyConcreteSchemaAttributes(oaProperties, concreteSchema);
            return;
        }

        if (ioaSchema is OpenApiSchemaReference refSchema)
        {
            ApplyReferenceSchemaAttributes(oaProperties, refSchema);
        }
    }

    /// <summary>
    /// Applies concrete schema attributes to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyConcreteSchemaAttributes(OpenApiProperties properties, OpenApiSchema schema)
    {
        ApplyTitleAndDescription(properties, schema);
        ApplySchemaType(properties, schema);
        ApplyFormatAndNumericBounds(properties, schema);
        ApplyLengthAndPattern(properties, schema);
        ApplyCollectionConstraints(properties, schema);
        ApplyFlags(properties, schema);
        ApplyExamplesAndDefaults(properties, schema);
    }

    /// <summary>
    /// Applies title and description to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyTitleAndDescription(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Title is not null)
        {
            schema.Title = properties.Title;
        }
        if (properties is not OpenApiParameterComponentAttribute && properties.Description is not null)
        {
            schema.Description = properties.Description;
        }
    }

    /// <summary>
    /// Applies schema type and nullability to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplySchemaType(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Type != OaSchemaType.None)
        {
            schema.Type = properties.Type switch
            {
                OaSchemaType.String => JsonSchemaType.String,
                OaSchemaType.Number => JsonSchemaType.Number,
                OaSchemaType.Integer => JsonSchemaType.Integer,
                OaSchemaType.Boolean => JsonSchemaType.Boolean,
                OaSchemaType.Array => JsonSchemaType.Array,
                OaSchemaType.Object => JsonSchemaType.Object,
                OaSchemaType.Null => JsonSchemaType.Null,
                _ => schema.Type
            };
        }

        if (properties.Nullable)
        {
            schema.Type |= JsonSchemaType.Null;
        }
    }

    /// <summary>
    /// Applies format and numeric bounds to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema"></param>
    private static void ApplyFormatAndNumericBounds(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (!string.IsNullOrWhiteSpace(properties.Format))
        {
            schema.Format = properties.Format;
        }

        if (properties.MultipleOf.HasValue)
        {
            schema.MultipleOf = properties.MultipleOf;
        }

        if (!string.IsNullOrWhiteSpace(properties.Maximum))
        {
            schema.Maximum = properties.Maximum;
            if (properties.ExclusiveMaximum)
            {
                schema.ExclusiveMaximum = properties.Maximum;
            }
        }

        if (!string.IsNullOrWhiteSpace(properties.Minimum))
        {
            schema.Minimum = properties.Minimum;
            if (properties.ExclusiveMinimum)
            {
                schema.ExclusiveMinimum = properties.Minimum;
            }
        }
    }

    /// <summary>
    /// Applies length and pattern constraints to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema"></param>
    private static void ApplyLengthAndPattern(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.MaxLength >= 0)
        {
            schema.MaxLength = properties.MaxLength;
        }

        if (properties.MinLength >= 0)
        {
            schema.MinLength = properties.MinLength;
        }

        if (!string.IsNullOrWhiteSpace(properties.Pattern))
        {
            schema.Pattern = properties.Pattern;
        }
    }

    /// <summary>
    /// Applies collection constraints to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyCollectionConstraints(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.MaxItems >= 0)
        {
            schema.MaxItems = properties.MaxItems;
        }

        if (properties.MinItems >= 0)
        {
            schema.MinItems = properties.MinItems;
        }

        if (properties.UniqueItems)
        {
            schema.UniqueItems = true;
        }

        if (properties.MaxProperties >= 0)
        {
            schema.MaxProperties = properties.MaxProperties;
        }

        if (properties.MinProperties >= 0)
        {
            schema.MinProperties = properties.MinProperties;
        }
    }

    private static void ApplyFlags(OpenApiProperties properties, OpenApiSchema schema)
    {
        schema.ReadOnly = properties.ReadOnly;
        schema.WriteOnly = properties.WriteOnly;
        schema.AdditionalPropertiesAllowed = properties.AdditionalPropertiesAllowed;
        schema.UnevaluatedProperties = properties.UnevaluatedProperties;
        if (properties is not OpenApiParameterComponentAttribute)
        {
            schema.Deprecated = properties.Deprecated;
        }
    }

    private static void ApplyExamplesAndDefaults(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Default is not null)
        {
            schema.Default = OpenApiJsonNodeFactory.ToNode(properties.Default);
        }
        if (properties.Example is not null && properties is not OpenApiParameterComponentAttribute)
        {
            schema.Example = OpenApiJsonNodeFactory.ToNode(properties.Example);
        }

        if (properties.Enum is { Length: > 0 })
        {
            schema.Enum = [.. properties.Enum.Select(OpenApiJsonNodeFactory.ToNode).OfType<JsonNode>()];
        }

        if (properties.RequiredProperties is { Length: > 0 })
        {
            schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in properties.RequiredProperties)
            {
                _ = schema.Required.Add(r);
            }
        }
    }

    /// <summary>
    /// Applies reference schema attributes to an OpenApiSchemaReference.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="reference">The OpenApiSchemaReference to apply attributes to.</param>
    private static void ApplyReferenceSchemaAttributes(OpenApiProperties properties, OpenApiSchemaReference reference)
    {
        // Description/Title can live on a reference proxy in v2 (and serialize alongside $ref)
        if (!string.IsNullOrWhiteSpace(properties.Description))
        {
            reference.Description = properties.Description;
        }

        if (!string.IsNullOrWhiteSpace(properties.Title))
        {
            reference.Title = properties.Title;
        }

        // Example/Default/Enum aren’t typically set on the ref node itself;
        // attach such metadata to the component target instead if you need it.
    }

    #endregion
}
