
using System.Collections;
using System.Management.Automation;
using System.Text.Json;
using System.Xml.Linq;
using Kestrun.Logging;
using Kestrun.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kestrun.Languages;

/// <summary>
/// Information about a parameter to be injected into a script.
/// </summary>
public class ParameterForInjectionInfo : ParameterForInjectionInfoBase
{
    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiParameter.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="parameter">The OpenApiParameter to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiParameter? parameter) :
        base(paramInfo.Name, paramInfo.ParameterType)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(paramInfo);
        Type = parameter.Schema?.Type;
        DefaultValue = parameter.Schema?.Default;
        In = parameter.In;
    }
    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiRequestBody.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="requestBody">The OpenApiRequestBody to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiRequestBody requestBody) :
        base(paramInfo.Name, paramInfo.ParameterType)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(paramInfo);
        Type = requestBody.Content?.Values.FirstOrDefault()?.Schema?.Type;
        var schema = requestBody.Content?.Values.FirstOrDefault()?.Schema;
        if (schema is OpenApiSchemaReference)
        {
            Type = JsonSchemaType.Object;
        }
        else if (schema is OpenApiSchema sch)
        {
            Type = sch.Type;
            DefaultValue = sch.Default;
        }
        In = null;
    }

    /// <summary>
    /// Adds parameters from the HTTP context to the PowerShell instance.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ps">The PowerShell instance to which parameters will be added.</param>
    internal static void InjectParameters(KestrunContext context, PowerShell ps)
    {
        if (context.HttpContext.GetEndpoint()?
               .Metadata
               .FirstOrDefault(m => m is List<ParameterForInjectionInfo>) is not List<ParameterForInjectionInfo> parameters)
        {
            return;
        }

        var logger = context.Host.Logger;
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Injecting {Count} parameters into PowerShell script.", parameters.Count);
        }

        foreach (var param in parameters)
        {
            InjectSingleParameter(context, ps, param);
        }
    }

    /// <summary>
    /// Injects a single parameter into the PowerShell instance based on its location and type.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="ps">The PowerShell instance to inject parameters into.</param>
    /// <param name="param">The parameter information to inject.</param>
    private static void InjectSingleParameter(KestrunContext context, PowerShell ps, ParameterForInjectionInfo param)
    {
        var logger = context.Host.Logger;
        var name = param.Name;

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Injecting parameter '{Name}' of type '{Type}' from '{In}'.", name, param.Type, param.In);
        }

        object? converted;
        var shouldLog = true;

        converted = context.Request.Form is not null && context.Request.HasFormContentType
            ? ConvertFormToValue(context.Request.Form, param)
            : GetParameterValueFromContext(context, param, out shouldLog);

        if (shouldLog && logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.DebugSanitized("Adding parameter '{Name}': {ConvertedValue}", name, converted);
        }

        _ = ps.AddParameter(name, converted);
        if (param.IsRequestBody)
        {
            context.Parameters.Body = new ParameterForInjectionResolved(param, converted);
        }
        else
        {
            context.Parameters.Parameters[name] = new ParameterForInjectionResolved(param, converted);
        }
    }

    private static object? GetParameterValueFromContext(KestrunContext context, ParameterForInjectionInfo param, out bool shouldLog)
    {
        shouldLog = true;
        var logger = context.Host.Logger;
        var raw = GetRawValue(param, context);

        if (raw is null)
        {
            if (param.DefaultValue is not null)
            {
                raw = param.DefaultValue.GetValue<object>();
            }
            else
            {
                shouldLog = false;
                return null;
            }
        }

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Raw value for parameter '{Name}': {RawValue}", param.Name, raw);
        }

        var (singleValue, multiValue) = NormalizeRaw(raw);

        if (singleValue is null && multiValue is null)
        {
            shouldLog = false;
            return null;
        }

        return ConvertValue(context, param, singleValue, multiValue);
    }

    /// <summary>
    /// Retrieves the raw value of a parameter from the HTTP context based on its location.
    /// </summary>
    /// <param name="param">The parameter information.</param>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The raw value of the parameter.</returns>
    private static object? GetRawValue(ParameterForInjectionInfo param, KestrunContext context)
    {
        return param.In switch
        {
            ParameterLocation.Path =>
            context.Request.RouteValues.TryGetValue(param.Name, out var routeVal)
                ? routeVal
                : null,

            ParameterLocation.Query =>
                context.Request.Query.TryGetValue(param.Name, out var queryVal)
                    ? (string?)queryVal
                    : null,

            ParameterLocation.Header =>
                context.Request.Headers.TryGetValue(param.Name, out var headerVal)
                    ? (string?)headerVal
                    : null,

            ParameterLocation.Cookie =>
                context.Request.Cookies.TryGetValue(param.Name, out var cookieVal)
                    ? cookieVal
                    : null,
            null => (context.Request.Form is not null && context.Request.HasFormContentType) ?
                    context.Request.Form :
                    context.Request.Body,

            _ => null,
        };
    }

    /// <summary>
    /// Normalizes the raw parameter value into single and multi-value forms.
    /// </summary>
    /// <param name="raw">The raw parameter value.</param>
    /// <returns>A tuple containing the single and multi-value forms of the parameter.</returns>
    private static (string? single, string?[]? multi) NormalizeRaw(object raw)
    {
        string?[]? multiValue = null;

        string? singleValue;
        switch (raw)
        {
            case StringValues sv:
                multiValue = [.. sv];
                singleValue = sv.Count > 0 ? sv[0] : null;
                break;

            case string s:
                singleValue = s;
                break;

            default:
                singleValue = raw?.ToString();
                break;
        }

        return (singleValue, multiValue);
    }

    /// <summary>
    /// Converts the parameter value to the appropriate type based on the JSON schema type.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="param">The parameter information.</param>
    /// <param name="singleValue">The single value of the parameter.</param>
    /// <param name="multiValue">The multi-value of the parameter.</param>
    /// <returns>The converted parameter value.</returns>
    private static object? ConvertValue(KestrunContext context, ParameterForInjectionInfo param,
    string? singleValue, string?[]? multiValue)
    {
        // Convert based on schema type
        return param.Type switch
        {
            JsonSchemaType.Integer => int.TryParse(singleValue, out var i) ? (int?)i : null,
            JsonSchemaType.Number => double.TryParse(singleValue, out var d) ? (double?)d : null,
            JsonSchemaType.Boolean => bool.TryParse(singleValue, out var b) ? (bool?)b : null,
            JsonSchemaType.Array => multiValue ?? (singleValue is not null ? new[] { singleValue } : null), // keep your existing behaviour for query/header multi-values
            JsonSchemaType.Object =>
                ConvertBodyBasedOnContentType(context, singleValue ?? ""),
            JsonSchemaType.String => singleValue,
            _ => singleValue,
        };
    }
    //todo: test Yaml and XML bodies
    private static object? ConvertBodyBasedOnContentType(
       KestrunContext context,
       string rawBodyString)
    {
        var contentType = context.Request.ContentType?.ToLowerInvariant() ?? "";

        if (contentType.Contains("json"))
        {
            return ConvertJsonToHashtable(rawBodyString);
        }

        if (contentType.Contains("yaml") || contentType.Contains("yml"))
        {
            return ConvertYamlToHashtable(rawBodyString);
        }

        if (contentType.Contains("xml"))
        {
            return ConvertXmlToHashtable(rawBodyString);
        }

        if (contentType.Contains("application/x-www-form-urlencoded"))
        {
            return ConvertFormToHashtable(context.Request.Form);
        }

        return rawBodyString; // fallback
    }

    private static readonly IDeserializer YamlDeserializer =
    new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static Hashtable? ConvertYamlToHashtable(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return null;
        }

        // Top-level YAML mapping → Hashtable
        var ht = YamlDeserializer.Deserialize<Hashtable>(yaml);
        return ht;
    }
    private static object? ConvertJsonToHashtable(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return JsonElementToClr(doc.RootElement);
    }

    private static object? JsonElementToClr(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ToHashtable(element),
            JsonValueKind.Array => ToArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => null
        };
    }

    private static Hashtable ToHashtable(JsonElement element)
    {
        var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            ht[prop.Name] = JsonElementToClr(prop.Value);
        }
        return ht;
    }

    private static object?[] ToArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(JsonElementToClr(item));
        }
        return [.. list];
    }

    private static object? ConvertXmlToHashtable(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        var root = XElement.Parse(xml);
        return XElementToClr(root);
    }

    private static object? XElementToClr(XElement element)
    {
        // If element has no children and no attributes → return primitive string
        if (!element.HasElements && !element.HasAttributes)
        {
            var trimmed = element.Value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);

        // Attributes as @name entries
        foreach (var attr in element.Attributes())
        {
            ht["@" + attr.Name.LocalName] = attr.Value;
        }

        // Children
        var childGroups = element.Elements().GroupBy(e => e.Name.LocalName);

        foreach (var group in childGroups)
        {
            var key = group.Key;
            var items = group.ToList();

            if (items.Count == 1)
            {
                // Single element → convert directly
                ht[key] = XElementToClr(items[0]);
            }
            else
            {
                // Multiple elements with same name → array
                var arr = new object?[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    arr[i] = XElementToClr(items[i]);
                }

                ht[key] = arr;
            }
        }

        return ht;
    }

    /// <summary>
    /// Converts a form dictionary to a hashtable.
    /// </summary>
    /// <param name="form">The form dictionary to convert.</param>
    /// <returns>A hashtable representing the form data.</returns>
    private static Hashtable? ConvertFormToHashtable(Dictionary<string, string>? form)
    {
        if (form is null || form.Count == 0)
        {
            return null;
        }

        var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in form)
        {
            // x-www-form-urlencoded in your case has a single value per key
            ht[kvp.Key] = kvp.Value;
        }

        return ht;
    }

    private static object? ConvertFormToValue(Dictionary<string, string>? form, ParameterForInjectionInfo param)
    {
        if (form is null || form.Count == 0)
        {
            return null;
        }

        // If the parameter is a simple type, return the first key if there's only one key-value pair
        // and it's a simple type (not an object or array)
        return param.Type is JsonSchemaType.Integer or JsonSchemaType.Number or JsonSchemaType.Boolean or JsonSchemaType.Array or JsonSchemaType.String
            ? form.Count == 1 ? form.First().Key : null
            : ConvertFormToHashtable(form);
    }
}
