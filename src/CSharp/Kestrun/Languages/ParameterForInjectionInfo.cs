
using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Text.Json;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Kestrun.Logging;
using Kestrun.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PeterO.Cbor;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kestrun.Languages;

/// <summary>
/// Information about a parameter to be injected into a script.
/// </summary>
public class ParameterForInjectionInfo : ParameterForInjectionInfoBase
{
    private static ParameterMetadata Validate(ParameterMetadata? paramInfo)
    {
        ArgumentNullException.ThrowIfNull(paramInfo);
        return paramInfo;
    }

    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiParameter.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="parameter">The OpenApiParameter to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiParameter? parameter) :
        base(Validate(paramInfo).Name, Validate(paramInfo).ParameterType)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Type = parameter.Schema?.Type;
        DefaultValue = parameter.Schema?.Default;
        In = parameter.In;
        if (parameter.Content is not null)
        {
            foreach (var key in parameter.Content.Keys)
            {
                ContentTypes.Add(key);
            }
        }
        else
        {
            Explode = parameter.Explode;
            Style = parameter.Style;
        }
    }
    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiRequestBody.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="requestBody">The OpenApiRequestBody to construct from.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiRequestBody requestBody) :
        base(Validate(paramInfo).Name, Validate(paramInfo).ParameterType)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
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
        if (requestBody.Content is not null)
        {
            foreach (var key in requestBody.Content.Keys)
            {
                ContentTypes.Add(key);
            }
        }
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
            var parType = (param.Type is null) ? param.ParameterType.GetType().FullName : param.Type.ToString();
            logger.Debug("Injecting parameter '{Name}' of type '{Type}' from '{In}'.", name, parType, param.In);
        }

        object? converted;
        var shouldLog = true;

        converted = context.Request.Form is not null && context.Request.HasFormContentType
            ? ConvertFormToValue(context.Request.Form, param)
            : GetParameterValueFromContext(context, param, out shouldLog);

        if (converted is not null && converted is string rawString && param.Type == null && param.ParameterType != null &&
            param.ParameterType != typeof(string))
        {
            if (param.ContentTypes.Any(ct =>
                 ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertJsonToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                ct.StartsWith("application/yaml", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertYamlToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                  ct.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertXmlToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                 ct.StartsWith("application/bson", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertBsonToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                 ct.StartsWith("application/cbor", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertCborToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                 ct.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase)))
            {
                converted = ConvertCsvToHashtable(rawString);
            }
            else if (param.ContentTypes.Any(ct =>
                 ct.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) && param.Explode)
            {
                // For 'form' style with 'explode', convert to hashtable
                converted = ConvertFormToHashtable(context.Request.Form);
            }
            else if (param.Style == ParameterStyle.Form && param.Explode)
            {
                // For 'form' style with 'explode', convert to hashtable
                converted = ConvertFormToHashtable(context.Request.Form);
            }
            else
            {
                context.Logger.WarningSanitized("Unable to convert body parameter '{Name}' with content types: {ContentTypes}. Using raw string value.", name, param.ContentTypes);
            }
        }
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

    /// <summary>
    /// Retrieves and converts the parameter value from the HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="param">The parameter information.</param>
    /// <param name="shouldLog">Indicates whether logging should be performed.</param>
    /// <returns>The converted parameter value.</returns>
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
            JsonSchemaType.Object => param.IsRequestBody
                                    ? ConvertBodyBasedOnContentType(context, singleValue ?? "", param)
                                    : singleValue,
            JsonSchemaType.String => singleValue,
            _ => singleValue,
        };
    }
    //todo: test Yaml and XML bodies
    private static object? ConvertBodyBasedOnContentType(
       KestrunContext context,
       string rawBodyString, ParameterForInjectionInfo param)
    {
        var lenient = param.ContentTypes.Count == 1;
        var contentType = context.Request.ContentType?.ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(contentType))
        {
            if (lenient)
            {
                // Try to infer content type
                if (param.ContentTypes[0].StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertJsonToHashtable(rawBodyString);
                }
                else if (param.ContentTypes[0].StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertXmlToHashtable(rawBodyString);
                }
                else if (param.ContentTypes[0].StartsWith("application/yaml", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertYamlToHashtable(rawBodyString);
                }
                else if (param.ContentTypes[0].StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertFormToHashtable(context.Request.Form);
                }
                else if (param.ContentTypes[0].StartsWith("application/bson", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertBsonToHashtable(rawBodyString);
                }
                else if (param.ContentTypes[0].StartsWith("application/cbor", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertCborToHashtable(rawBodyString);
                }
                else if (param.ContentTypes[0].StartsWith("text/csv", StringComparison.OrdinalIgnoreCase))
                {
                    return ConvertCsvToHashtable(rawBodyString);
                }
                else
                {
                    return rawBodyString; // fallback
                }
            }
            else
            {
                throw new InvalidOperationException("Content-Type header is missing; cannot convert body to object.");
            }
        }

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

        if (contentType.Contains("bson"))
        {
            return ConvertBsonToHashtable(rawBodyString);
        }

        if (contentType.Contains("cbor"))
        {
            return ConvertCborToHashtable(rawBodyString);
        }

        if (contentType.Contains("csv"))
        {
            return ConvertCsvToHashtable(rawBodyString);
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

    private static object? ConvertBsonToHashtable(string bson)
    {
        if (string.IsNullOrWhiteSpace(bson))
        {
            return null;
        }

        var bytes = DecodeBodyStringToBytes(bson);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var doc = BsonSerializer.Deserialize<BsonDocument>(bytes);
        return BsonValueToClr(doc);
    }

    private static object? BsonValueToClr(BsonValue value)
    {
        return value is null || value.IsBsonNull
            ? null
            : value.BsonType switch
            {
                BsonType.Document => BsonDocumentToHashtable(value.AsBsonDocument),
                BsonType.Array => BsonArrayToClrArray(value.AsBsonArray),
                BsonType.Boolean => value.AsBoolean,
                BsonType.Int32 => value.AsInt32,
                BsonType.Int64 => value.AsInt64,
                BsonType.Double => value.AsDouble,
                BsonType.Decimal128 => value.AsDecimal,
                BsonType.String => value.AsString,
                BsonType.DateTime => value.ToUniversalTime(),
                BsonType.ObjectId => value.AsObjectId.ToString(),
                BsonType.Binary => value.AsBsonBinaryData.Bytes,
                BsonType.Null => null,
                _ => value.ToString(),
            };
    }

    private static Hashtable BsonDocumentToHashtable(BsonDocument doc)
    {
        var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc.Elements)
        {
            ht[element.Name] = BsonValueToClr(element.Value);
        }

        return ht;
    }

    private static object?[] BsonArrayToClrArray(BsonArray arr)
    {
        var list = new object?[arr.Count];
        for (var i = 0; i < arr.Count; i++)
        {
            list[i] = BsonValueToClr(arr[i]);
        }

        return list;
    }

    private static object? ConvertCborToHashtable(string cbor)
    {
        if (string.IsNullOrWhiteSpace(cbor))
        {
            return null;
        }

        var bytes = DecodeBodyStringToBytes(cbor);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var obj = CBORObject.DecodeFromBytes(bytes);
        return CborToClr(obj);
    }

    private static object? CborToClr(CBORObject obj)
    {
        if (obj is null)
        {
            return null;
        }

        if (obj.IsNull)
        {
            return null;
        }

        if (obj.Type == CBORType.Map)
        {
            var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var key in obj.Keys)
            {
                var keyString = key?.ToString() ?? string.Empty;
                ht[keyString] = CborToClr(obj[key]);
            }

            return ht;
        }

        if (obj.Type == CBORType.Array)
        {
            var list = new object?[obj.Count];
            for (var i = 0; i < obj.Count; i++)
            {
                list[i] = CborToClr(obj[i]);
            }

            return list;
        }

        // Scalar conversions
        if (obj.IsNumber)
        {
            // Prefer integral if representable; else double/decimal as available.
            var number = obj.AsNumber();
            if (number.CanFitInInt64())
            {
                return number.ToInt64Checked();
            }

            if (number.CanFitInDouble())
            {
                return obj.ToObject(typeof(double));
            }

            // For extremely large/precise numbers, keep a string representation.
            return number.ToString();
        }

        if (obj.Type == CBORType.Boolean)
        {
            return obj.AsBoolean();
        }

        if (obj.Type == CBORType.ByteString)
        {
            return obj.GetByteString();
        }

        // TextString, SimpleValue, etc.
        return obj.Type switch
        {
            CBORType.TextString => obj.AsString(),
            CBORType.SimpleValue => obj.ToString(),
            _ => obj.ToString(),
        };
    }

    private static object? ConvertCsvToHashtable(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        using var reader = new StringReader(csv);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
        };

        using var csvReader = new CsvReader(reader, config);
        var records = new List<Hashtable>();

        foreach (var rec in csvReader.GetRecords<dynamic>())
        {
            if (rec is not IDictionary<string, object?> dict)
            {
                continue;
            }

            var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dict)
            {
                ht[kvp.Key] = kvp.Value;
            }

            records.Add(ht);
        }

        return records.Count == 0
            ? null
            : (records.Count == 1 ? records[0] : records.Cast<object?>().ToArray());
    }

    private static byte[]? DecodeBodyStringToBytes(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var trimmed = body.Trim();
        if (trimmed.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["base64:".Length..].Trim();
        }

        if (TryDecodeBase64(trimmed, out var base64Bytes))
        {
            return base64Bytes;
        }

        if (TryDecodeHex(trimmed, out var hexBytes))
        {
            return hexBytes;
        }

        // Fallback: interpret as UTF-8 text (best-effort).
        return System.Text.Encoding.UTF8.GetBytes(trimmed);
    }

    private static bool TryDecodeBase64(string input, out byte[] bytes)
    {
        bytes = [];

        // Quick reject for non-base64 strings.
        if (input.Length < 4 || (input.Length % 4) != 0)
        {
            return false;
        }

        // Avoid throwing on clearly non-base64 content.
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            var isValid =
                c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/' or '=' or '\r' or '\n';

            if (!isValid)
            {
                return false;
            }
        }

        try
        {
            bytes = Convert.FromBase64String(input);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDecodeHex(string input, out byte[] bytes)
    {
        bytes = [];
        var s = input.Trim();

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }

        if (s.Length < 2 || (s.Length % 2) != 0)
        {
            return false;
        }

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        bytes = new byte[s.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        }

        return true;
    }
}
