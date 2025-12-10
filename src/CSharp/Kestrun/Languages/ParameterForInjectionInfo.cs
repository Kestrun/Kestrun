
using System.Collections;
using System.Management.Automation;
using System.Text.Json;
using System.Text.Json.Nodes;
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
public record ParameterForInjectionInfo
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The .NET type of the parameter.
    /// </summary>
    public Type ParameterType { get; }

    /// <summary>
    /// The JSON schema type of the parameter.
    /// </summary>
    public JsonSchemaType? Type { get; init; }

    /// <summary>
    /// The default value of the parameter.
    /// </summary>
    public JsonNode? DefaultValue { get; }

    /// <summary>
    /// The location of the parameter.
    /// </summary>
    public ParameterLocation? In { get; init; }

    /// <summary>
    /// Indicates whether the parameter is from the request body.
    /// </summary>
    public bool IsRequestBody => In is null;

    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiParameter.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="parameter">The OpenApiParameter to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiParameter? parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(paramInfo);
        Name = paramInfo.Name;
        ParameterType = paramInfo.ParameterType;
        Type = parameter.Schema?.Type;
        DefaultValue = parameter.Schema?.Default;
        In = parameter.In;
    }
    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiRequestBody.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="requestBody">The OpenApiRequestBody to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiRequestBody requestBody)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(paramInfo);
        Name = paramInfo.Name;
        ParameterType = paramInfo.ParameterType;
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
        var parameters = context.HttpContext.GetEndpoint()?
               .Metadata
               .FirstOrDefault(m => m is List<ParameterForInjectionInfo>)
               as List<ParameterForInjectionInfo>;
        var logger = context.Host.Logger;
        if (parameters is not null)
        {
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Injecting {Count} parameters into PowerShell script.", parameters.Count);
            }

            foreach (var param in parameters)
            {
                var name = param.Name;
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug("Injecting parameter '{Name}' of type '{Type}' from '{In}'.", name, param.Type, param.In);
                }
                // Initialize the converted value
                object? converted = null;
                if (context.Request.Form is not null && context.Request.HasFormContentType)
                {
                    // For form data, convert the entire form to a hashtable
                    converted = ConvertFormToHashtable(context.Request.Form);
                }
                else
                {
                    // Retrieve the raw value from the HTTP context
                    var raw = GetRawValue(param, context);

                    if (raw is null)
                    {
                        if (param.DefaultValue is not null)
                        {
                            raw = param.DefaultValue.GetValue<object>();
                        }
                        else
                        {
                            _ = ps.AddParameter(name, null);
                            continue;
                        }
                    }

                    if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                    {
                        logger.Debug("Raw value for parameter '{Name}': {RawValue}", name, raw);
                    }

                    var (singleValue, multiValue) = NormalizeRaw(raw);

                    if (singleValue is null && multiValue is null)
                    {
                        _ = ps.AddParameter(name, null);
                        continue;
                    }
                    // Convert the value based on the parameter's JSON schema type
                    converted = ConvertValue(context, param, singleValue, multiValue);
                }

                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.DebugSanitized("Adding parameter '{Name}': {ConvertedValue}", name, converted);
                }
                // Add the converted parameter to the PowerShell instance
                _ = ps.AddParameter(name, converted);
            }
        }
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
}
