
using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Text.Json;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Kestrun.Logging;
using Kestrun.Models;
using Kestrun.Utilities;
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
    /// Mapping of content types to body conversion functions.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<KestrunContext, string, object?>> BodyConverters =
        new Dictionary<string, Func<KestrunContext, string, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/json"] = (_, raw) => ConvertJsonToHashtable(raw),
            ["application/yaml"] = (_, raw) => ConvertYamlToHashtable(raw),
            ["application/xml"] = (_, raw) => ConvertXmlToHashtable(raw),
            ["application/bson"] = (_, raw) => ConvertBsonToHashtable(raw),
            ["application/cbor"] = (_, raw) => ConvertCborToHashtable(raw),
            ["text/csv"] = (_, raw) => ConvertCsvToHashtable(raw),

            // This one typically needs the request form, not the raw string.
            ["application/x-www-form-urlencoded"] = (ctx, _) => ConvertFormToHashtable(ctx.Request.Form),
        };

    /// <summary>
    /// Determines whether the body parameter should be converted based on its type information.
    /// </summary>
    /// <param name="param">The parameter information.</param>
    /// <param name="converted">The converted object.</param>
    /// <returns>True if the body should be converted; otherwise, false.</returns>
    private static bool ShouldConvertBody(ParameterForInjectionInfo param, object? converted) =>
    converted is string && param.Type is null && param.ParameterType is not null && param.ParameterType != typeof(string);

    /// <summary>
    /// Tries to convert the body parameter based on the content types specified.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="param">The parameter information.</param>
    /// <param name="rawString">The raw body string.</param>
    private static object? TryConvertBodyByContentType(KestrunContext context, ParameterForInjectionInfo param, string rawString)
    {
        // Collect canonical content types once
        var canonicalTypes = param.ContentTypes
            .Select(MediaTypeHelper.Canonicalize)
            .Where(ct => !string.IsNullOrWhiteSpace(ct))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var ct in canonicalTypes)
        {
            if (BodyConverters.TryGetValue(ct, out var converter))
            {
                // Special-case: form-url-encoded conversion only makes sense with explode/form style.
                if (ct.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) &&
                    !(param.Explode || param.Style == ParameterStyle.Form))
                {
                    continue;
                }

                return converter(context, rawString);
            }
        }

        // If it's "form" style explode, you can still treat it as a hashtable even without explicit content-type.
        return param.Style == ParameterStyle.Form && param.Explode && context.Request.Form is not null
            ? ConvertFormToHashtable(context.Request.Form)
            : (object?)null;
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
            // (Tiny bugfix: ParameterType.GetType() would always be RuntimeType.)
            var parType = param.Type?.ToString() ?? param.ParameterType?.FullName ?? "<unknown>";
            logger.Debug("Injecting parameter '{Name}' of type '{Type}' from '{In}'.", name, parType, param.In);
        }

        var shouldLog = true;

        var converted =
            context.Request.Form is not null && context.Request.HasFormContentType
                ? ConvertFormToValue(context.Request.Form, param)
                : GetParameterValueFromContext(context, param, out shouldLog);

        if (ShouldConvertBody(param, converted))
        {
            var rawString = (string)converted!;
            var bodyObj = TryConvertBodyByContentType(context, param, rawString);

            if (bodyObj is not null)
            {
                converted = bodyObj;
            }
            else
            {
                context.Logger.WarningSanitized(
                    "Unable to convert body parameter '{Name}' with content types: {ContentTypes}. Using raw string value.",
                    name,
                    param.ContentTypes);
            }
        }

        if (shouldLog && logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.DebugSanitized("Adding parameter '{Name}': {ConvertedValue}", name, converted);
        }

        _ = ps.AddParameter(name, converted);

        var resolved = new ParameterForInjectionResolved(param, converted);
        if (param.IsRequestBody)
        {
            context.Parameters.Body = resolved;
        }
        else
        {
            context.Parameters.Parameters[name] = resolved;
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

    /// <summary>
    /// Converts the request body based on the Content-Type header.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="rawBodyString">The raw body string from the request.</param>
    /// <param name="param">The parameter information.</param>
    /// <returns>The converted body object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Content-Type header is missing and cannot convert body to object.</exception>
    private static object? ConvertBodyBasedOnContentType(
        KestrunContext context,
        string rawBodyString,
        ParameterForInjectionInfo param)
    {
        var lenient = param.ContentTypes.Count == 1;

        var requestMediaType = MediaTypeHelper.Canonicalize(context.Request.ContentType);

        if (string.IsNullOrEmpty(requestMediaType))
        {
            if (!lenient)
            {
                throw new InvalidOperationException(
                    "Content-Type header is missing; cannot convert body to object.");
            }

            var inferred = MediaTypeHelper.Canonicalize(param.ContentTypes[0]);
            return ConvertByCanonicalMediaType(inferred, context, rawBodyString);
        }

        return ConvertByCanonicalMediaType(requestMediaType, context, rawBodyString);
    }

    /// <summary>
    /// Converts the body string to an object based on the canonical media type.
    /// </summary>
    /// <param name="canonicalMediaType">   The canonical media type of the request body.</param>
    /// <param name="context"> The current Kestrun context.</param>
    /// <param name="rawBodyString"> The raw body string from the request.</param>
    /// <returns> The converted body object.</returns>
    private static object? ConvertByCanonicalMediaType(
        string canonicalMediaType,
        KestrunContext context,
        string rawBodyString)
    {
        return canonicalMediaType switch
        {
            "application/json" => ConvertJsonToHashtable(rawBodyString),
            "application/yaml" => ConvertYamlToHashtable(rawBodyString),
            "application/xml" => ConvertXmlToHashtable(rawBodyString),
            "application/bson" => ConvertBsonToHashtable(rawBodyString),
            "application/cbor" => ConvertCborToHashtable(rawBodyString),
            "text/csv" => ConvertCsvToHashtable(rawBodyString),
            "application/x-www-form-urlencoded" =>
                ConvertFormToHashtable(context.Request.Form),
            _ => rawBodyString,
        };
    }

    /// <summary>
    /// CBOR deserializer instance.
    /// </summary>
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

    /// <summary>
    /// Converts a CBORObject to a CLR object (Hashtable, array, or scalar).
    /// </summary>
    /// <param name="obj">The CBORObject to convert.</param>
    /// <returns>A CLR object representation of the CBORObject.</returns>
    private static object? CborToClr(CBORObject obj)
    {
        return obj is null || obj.IsNull
            ? null
            : obj.Type switch
            {
                CBORType.Map => ConvertCborMapToHashtable(obj),
                CBORType.Array => ConvertCborArrayToClrArray(obj),
                _ => ConvertCborScalarToClr(obj),
            };
    }

    /// <summary>
    /// Converts a CBOR map into a CLR <see cref="Hashtable"/>.
    /// </summary>
    /// <param name="map">The CBOR object expected to be of type <see cref="CBORType.Map"/>.</param>
    /// <returns>A case-insensitive hashtable representing the map.</returns>
    private static Hashtable ConvertCborMapToHashtable(CBORObject map)
    {
        var ht = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (var key in map.Keys)
        {
            var keyString = GetCborMapKeyString(key);
            ht[keyString] = CborToClr(map[key]);
        }

        return ht;
    }

    /// <summary>
    /// Converts a CBOR array into a CLR object array.
    /// </summary>
    /// <param name="array">The CBOR object expected to be of type <see cref="CBORType.Array"/>.</param>
    /// <returns>An array of converted elements.</returns>
    private static object?[] ConvertCborArrayToClrArray(CBORObject array)
    {
        var list = new object?[array.Count];
        for (var i = 0; i < array.Count; i++)
        {
            list[i] = CborToClr(array[i]);
        }

        return list;
    }

    /// <summary>
    /// Converts a CBOR scalar value (number, string, boolean, byte string, etc.) into a CLR value.
    /// </summary>
    /// <param name="scalar">The CBOR scalar to convert.</param>
    /// <returns>The converted CLR value.</returns>
    private static object? ConvertCborScalarToClr(CBORObject scalar)
    {
        if (scalar.IsNumber)
        {
            // Prefer integral if representable; else double/decimal as available.
            var number = scalar.AsNumber();
            if (number.CanFitInInt64())
            {
                return number.ToInt64Checked();
            }

            if (number.CanFitInDouble())
            {
                return scalar.ToObject<double>();
            }

            // For extremely large/precise numbers, keep a string representation.
            return number.ToString();
        }

        if (scalar.Type == CBORType.Boolean)
        {
            return scalar.AsBoolean();
        }

        if (scalar.Type == CBORType.ByteString)
        {
            return scalar.GetByteString();
        }

        // TextString, SimpleValue, etc.
        return scalar.Type switch
        {
            CBORType.TextString => scalar.AsString(),
            CBORType.SimpleValue => scalar.ToString(),
            _ => scalar.ToString(),
        };
    }

    /// <summary>
    /// Converts a CBOR map key into a CLR string key.
    /// </summary>
    /// <param name="key">The CBOR key object.</param>
    /// <returns>A best-effort string representation of the key.</returns>
    private static string GetCborMapKeyString(CBORObject? key)
    {
        return key is not null && key.Type == CBORType.TextString
            ? key.AsString()
            : (key?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Converts a CSV string to a hashtable or array of hashtables.
    /// </summary>
    /// <param name="csv">The CSV string to convert.</param>
    /// <returns>A hashtable if one record is present, an array of hashtables if multiple records are present, or null if no records are found.</returns>
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
