
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Reflection;
using System.Text.Json.Nodes;
using Kestrun.OpenApi;
using Microsoft.OpenApi;

internal static class PowerShellAttributes
{
    /// <summary>
    /// Applies PowerShell CmdletMetadataAttribute validations to an OpenAPI schema.
    /// </summary>
    /// <param name="attr">The CmdletMetadataAttribute to apply.</param>
    /// <param name="schema">The OpenAPI schema to modify.</param>
    internal static void ApplyPowerShellAttributes(CmdletMetadataAttribute attr, OpenApiSchema schema)
    {
        _ = attr switch
        {
            ValidateRangeAttribute a => ApplyValidateRangeAttribute(a, schema),

            ValidateLengthAttribute a => ApplyValidateLengthAttribute(a, schema),

            ValidateSetAttribute a => ApplyValidateSetAttribute(a, schema),

            ValidatePatternAttribute a => ApplyValidatePatternAttribute(a, schema),

            ValidateCountAttribute a => ApplyValidateCountAttribute(a, schema),

            ValidateNotNullOrEmptyAttribute => ApplyNotNullOrEmpty(schema),
            ValidateNotNullAttribute => ApplyNotNull(schema),
            ValidateNotNullOrWhiteSpaceAttribute => ApplyNotNullOrWhiteSpace(schema),
            _ => null
        };
    }

    /// <summary>
    /// Applies PowerShell validation attributes to the specified schema.
    /// </summary>
    /// <param name="p">The property info to inspect for validation attributes.</param>
    /// <param name="s">The OpenAPI schema to apply constraints to.</param>
    internal static void ApplyPowerShellValidationAttributes(PropertyInfo p, IOpenApiSchema s)
    {
        if (s is not OpenApiSchema sc)
        {
            return; // constraints only applicable on a concrete schema, not a $ref proxy
        }

        foreach (var attr in p.GetCustomAttributes(inherit: false))
        {
            var atName = attr.GetType().Name;
            switch (atName)
            {
                case "ValidateRangeAttribute":
                    {
                        _ = ApplyValidateRangeAttribute((ValidateRangeAttribute)attr, sc);
                        break;
                    }
                case "ValidateLengthAttribute":
                    {
                        _ = ApplyValidateLengthAttribute((ValidateLengthAttribute)attr, sc);
                        break;

                    }
                case "ValidateSetAttribute":
                    {
                        _ = ApplyValidateSetAttribute((ValidateSetAttribute)attr, sc);
                        break;
                    }

                case "ValidatePatternAttribute":
                    {
                        _ = ApplyValidatePatternAttribute((ValidatePatternAttribute)attr, sc);
                        break;
                    }

                case "ValidateCountAttribute":
                    {
                        _ = ApplyValidateCountAttribute((ValidateCountAttribute)attr, sc);
                        break;
                    }

                case "ValidateNotNullOrEmptyAttribute":
                    {
                        _ = ApplyNotNullOrEmpty(sc);
                        break;
                    }
                case "ValidateNotNullOrWhiteSpaceAttribute":
                    {
                        _ = ApplyNotNullOrWhiteSpace(sc);
                        break;
                    }
                case "ValidateNotNullAttribute":
                    {
                        _ = ApplyNotNull(sc);
                        break;
                    }
                default:
                    break;
            }
        }
    }

    private static object? ApplyValidateRangeAttribute(ValidateRangeAttribute attr, OpenApiSchema schema)
    {
        var min = attr.MinRange;
        var max = attr.MaxRange;
        if (min is not null)
        {
            schema.Minimum = min.ToString();
        }
        if (max is not null)
        {
            schema.Maximum = max.ToString();
        }
        return null;
    }

    private static object? ApplyValidateLengthAttribute(ValidateLengthAttribute attr, OpenApiSchema schema)
    {
        var minLen = attr.MinLength;
        var maxLen = attr.MaxLength;
        if (minLen >= 0)
        {
            schema.MinLength = minLen;
        }
        if (maxLen >= 0)
        {
            schema.MaxLength = maxLen;
        }
        return null;
    }

    private static object? ApplyValidateSetAttribute(ValidateSetAttribute attr, OpenApiSchema sc)
    {
        var vals = attr.ValidValues;
        if (vals is not null)
        {
            var list = new List<JsonNode>();
            foreach (var v in vals)
            {
                var node = OpenApiDocDescriptor.ToNode(v);
                if (node is not null)
                {
                    list.Add(node);
                }
            }
            if (list.Count > 0)
            {
                var existing = sc.Enum?.ToList() ?? [];
                existing.AddRange(list);
                sc.Enum = existing;
            }
        }
        return null;
    }
    private static object? ApplyValidatePatternAttribute(ValidatePatternAttribute attr, OpenApiSchema sc)
    {
        if (!string.IsNullOrWhiteSpace(sc.Pattern))
        {
            sc.Pattern = attr.RegexPattern;
        }
        return null;
    }

    private static object? ApplyValidateCountAttribute(ValidateCountAttribute attr, OpenApiSchema sc)
    {
        if (attr.MinLength >= 0)
        {
            sc.MinItems = attr.MinLength;
        }

        if (attr.MaxLength >= 0)
        {
            sc.MaxItems = attr.MaxLength;
        }
        return null;
    }


    private static object? ApplyNotNullOrEmpty(OpenApiSchema sc)
    {
        // string → minLength >= 1
        if (sc.Type == JsonSchemaType.String)
        {
            if (sc.MinLength is null or < 1)
            {
                sc.MinLength = 1;
            }
        }

        // array → minItems >= 1
        if (sc.Type == JsonSchemaType.Array)
        {
            if (sc.MinItems is null or < 1)
            {
                sc.MinItems = 1;
            }
        }

        return null;
    }


    private static object? ApplyNotNullOrWhiteSpace(OpenApiSchema sc)
    {
        if (sc.Type == JsonSchemaType.String)
        {
            if (sc.MinLength is null or < 1)
            {
                sc.MinLength = 1;
            }
            if (string.IsNullOrEmpty(sc.Pattern))
            {
                // No existing pattern → just require a non-whitespace character
                sc.Pattern = @"\S";
            }

        }
        return null;
    }


    private static object? ApplyNotNull(OpenApiSchema schema) => ApplyNotNullOrEmpty(schema);

}
