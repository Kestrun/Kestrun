using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using System.Runtime.Serialization;
using Kestrun.Forms;
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
    /// <summary>
    /// Content types associated with the parameter.
    /// </summary>
    /// <remarks>Typically used for body parameters.</remarks>
    private sealed class PatternPropertyRule(string keyPattern, string? schemaTypeName)
    {
        /// <summary>
        /// Regex pattern to match property keys.
        /// </summary>
        public string KeyPattern { get; } = keyPattern;

        /// <summary>
        /// Type name of the schema for matched properties.
        /// </summary>
        public string? SchemaTypeName { get; } = schemaTypeName;

        /// <summary>
        /// Determines if the given key matches the pattern.
        /// </summary>
        public bool IsMatch(string key)
            => Regex.IsMatch(key, KeyPattern, RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Validates the ParameterMetadata instance.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata to validate.</param>
    /// <returns>The validated ParameterMetadata.</returns>
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
    /// <param name="formOptions">The form options for handling form data.</param>
    public ParameterForInjectionInfo(ParameterMetadata paramInfo, OpenApiRequestBody requestBody, KrFormOptions? formOptions) :
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
        // Default to "form" style with explode for request bodies.
        FormOptions = formOptions;
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
            // XML conversion needs to consider OpenAPI XML modeling; handled in the callers that have ParameterType.
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
        // Try each content type in order
        foreach (var ct in canonicalTypes)
        {
            if (ct.Equals("application/xml", StringComparison.OrdinalIgnoreCase))
            {
                return ConvertXmlBodyToParameterType(rawString, param.ParameterType);
            }

            if (BodyConverters.TryGetValue(ct, out var converter))
            {
                // Special-case: form-url-encoded conversion only makes sense with explode/form style.
                if (ct.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) &&
                    !(param.Explode || param.Style == ParameterStyle.Form))
                {
                    continue;
                }
                // Use the converter
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
        object? converted;
        LogInjectingParameter(logger, param);
        if (param.FormOptions is not null)
        {
            converted = KrFormParser.Parse(context.HttpContext, param.FormOptions, context.Ct);
            converted = CoerceFormPayloadForParameter(param.ParameterType, converted, logger, ps);
        }
        else
        {
            converted = GetConvertedParameterValue(context, param, out var shouldLog);
            converted = ConvertBodyParameterIfNeeded(context, param, converted);
            LogAddingParameter(logger, name, converted, shouldLog);
        }

        if (converted is Hashtable ht && param.ParameterType is not null)
        {
            var coerced = TryConvertHashtableToParameterType(param.ParameterType, ht, logger, ps);
            if (coerced is not null)
            {
                converted = coerced;
            }
        }
        _ = ps.AddParameter(name, converted);
        StoreResolvedParameter(context, param, name, converted);
    }

    /// <summary>
    /// Coerces a parsed form payload to the expected parameter type when possible.
    /// This supports parameters typed as <see cref="IKrFormPayload"/> as well as PowerShell
    /// classes derived from <see cref="KrMultipart"/>.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="payload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>The original payload or a new instance compatible with the parameter type.</returns>
    internal static object? CoerceFormPayloadForParameter(Type? parameterType, object? payload, Serilog.ILogger logger, PowerShell? ps = null)
    {
        if (parameterType is null || payload is not IKrFormPayload formPayload)
        {
            return payload;
        }

        LogCoerceFormPayload(logger, parameterType, payload, ps);

        var exactMatch = TryHandleExactPayloadType(parameterType, formPayload, logger, ps);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var boundModel = TryBindFormModel(parameterType, formPayload, logger, ps);
        if (boundModel is not null)
        {
            return boundModel;
        }

        var coerced = TryCoerceToKrFormPayload(parameterType, formPayload, logger, ps);
        return coerced ?? formPayload;
    }

    /// <summary>
    /// Logs form payload coercion details when debug logging is enabled.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="parameterType">The parameter type being targeted.</param>
    /// <param name="payload">The raw payload instance.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    private static void LogCoerceFormPayload(
        Serilog.ILogger logger,
        Type parameterType,
        object? payload,
        PowerShell? ps)
    {
        if (!logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            return;
        }

        var runspaceId = ps?.Runspace?.InstanceId.ToString() ?? "<null>";
        logger.Debug(
            "CoerceFormPayloadForParameter: parameterType={ParameterType} payloadType={PayloadType} runspaceId={RunspaceId}",
            parameterType,
            payload?.GetType(),
            runspaceId);
    }

    /// <summary>
    /// Handles the case where the parsed payload already matches the parameter type.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formPayload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>The payload instance when it already matches the target type; otherwise null.</returns>
    private static object? TryHandleExactPayloadType(
        Type parameterType,
        IKrFormPayload formPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (!parameterType.IsInstanceOfType(formPayload))
        {
            return null;
        }

        // The parser may have already constructed the exact derived payload type.
        // Still populate any declared KrMultipart-derived properties (outer/nested/etc) from Parts.
        if (formPayload is KrMultipart mp)
        {
            TryPopulateKrMultipartDerivedModel(mp, logger, ps);
        }

        return formPayload;
    }

    /// <summary>
    /// Attempts to bind a KrBindForm model that does not inherit from payload base classes.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formPayload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>A bound model instance when binding succeeds; otherwise null.</returns>
    private static object? TryBindFormModel(
        Type parameterType,
        IKrFormPayload formPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        // Support KrBindForm on types that do not inherit KrMultipart/KrFormData.
        // The form kind is inferred from MaxNestingDepth (multipart when > 0; otherwise form-data).
        var bindAttr = parameterType.GetCustomAttribute<KrBindFormAttribute>(inherit: true);
        if (bindAttr is null)
        {
            return null;
        }
        // Decide which binding strategy to use based on MaxNestingDepth.
        return bindAttr.MaxNestingDepth > 0
            ? TryBindMultipartModel(parameterType, formPayload as KrMultipart, logger, ps)
            : TryBindFormDataModel(parameterType, formPayload as KrFormData, logger, ps);
    }

    /// <summary>
    /// Attempts to bind a multipart model from a multipart payload.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="multipartPayload">The multipart payload, if available.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>A bound model instance when binding succeeds; otherwise null.</returns>
    private static object? TryBindMultipartModel(
        Type parameterType,
        KrMultipart? multipartPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (multipartPayload is null)
        {
            return null;
        }

        var instance = TryCreateObjectInstance(parameterType, logger, ps);
        if (instance is null)
        {
            logger.Warning("Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            return null;
        }

        var instanceType = instance.GetType();
        TryPopulateMultipartStorage(instance, multipartPayload, logger);
        TryPopulateMultipartObjectProperties(instance, multipartPayload, instanceType, logger, ps, depth: 0);
        return instance;
    }

    /// <summary>
    /// Attempts to bind a form-data model from a form-data payload.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formDataPayload">The form-data payload, if available.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>A bound model instance when binding succeeds; otherwise null.</returns>
    private static object? TryBindFormDataModel(
        Type parameterType,
        KrFormData? formDataPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (formDataPayload is null)
        {
            return null;
        }

        var instance = TryCreateObjectInstance(parameterType, logger, ps);
        if (instance is null)
        {
            logger.Warning("Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            return null;
        }

        var instanceType = instance.GetType();
        TryPopulateFormDataObjectProperties(instance, formDataPayload, instanceType, logger);
        return instance;
    }

    /// <summary>
    /// Attempts to coerce a parsed payload into KrFormData or KrMultipart derived types.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formPayload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>The coerced payload instance when conversion succeeds; otherwise null.</returns>
    private static object? TryCoerceToKrFormPayload(
        Type parameterType,
        IKrFormPayload formPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        var formDataResult = TryCoerceToKrFormData(parameterType, formPayload, logger, ps);
        if (formDataResult is not null)
        {
            return formDataResult;
        }
        // Try multipart next
        return TryCoerceToKrMultipart(parameterType, formPayload, logger, ps);
    }

    /// <summary>
    /// Attempts to coerce a parsed payload into a <see cref="KrFormData"/> derived type.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formPayload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>A derived form-data instance when conversion succeeds; otherwise null.</returns>
    private static KrFormData? TryCoerceToKrFormData(
        Type parameterType,
        IKrFormPayload formPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (formPayload is not KrFormData formData || !typeof(KrFormData).IsAssignableFrom(parameterType))
        {
            return null;
        }

        var instance = IsPowerShellClassType(parameterType) && ps is not null
            ? TryCreateFormDataInstanceInRunspace(ps, parameterType, logger)
            : TryCreateFormDataInstance(parameterType, logger);

        if (instance is null)
        {
            return null;
        }

        CopyFormData(instance, formData);
        return instance;
    }

    /// <summary>
    /// Attempts to coerce a parsed payload into a <see cref="KrMultipart"/> derived type.
    /// </summary>
    /// <param name="parameterType">The parameter type to satisfy.</param>
    /// <param name="formPayload">The parsed form payload.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, when available.</param>
    /// <returns>A derived multipart instance when conversion succeeds; otherwise null.</returns>
    private static KrMultipart? TryCoerceToKrMultipart(
        Type parameterType,
        IKrFormPayload formPayload,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (formPayload is not KrMultipart multipart || !typeof(KrMultipart).IsAssignableFrom(parameterType))
        {
            return null;
        }

        var instance = IsPowerShellClassType(parameterType) && ps is not null
            ? TryCreateMultipartInstanceInRunspace(ps, parameterType, logger)
            : TryCreateMultipartInstance(parameterType, logger);

        if (instance is null)
        {
            return null;
        }

        instance.Parts.AddRange(multipart.Parts);
        TryPopulateKrMultipartDerivedModel(instance, logger, ps);
        return instance;
    }

    /// <summary>
    /// Attempts to populate properties declared on a <see cref="KrMultipart"/>-derived model
    /// from the underlying ordered <see cref="KrMultipart.Parts"/> list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The multipart parser produces a runtime container (<see cref="KrMultipart"/>) with ordered parts.
    /// This helper performs a best-effort mapping for properties decorated with <see cref="KrPartAttribute"/>
    /// so PowerShell model classes (e.g., <c>NestedMultipartRequest : KrMultipart</c>) expose populated
    /// <c>$outer</c>/<c>$nested</c> properties in addition to the raw <c>Parts</c> list.
    /// </para>
    /// <para>
    /// Mapping is intentionally conservative: unknown shapes are skipped rather than causing request failure.
    /// Validation/limits are still enforced by the parser via rules.
    /// </para>
    /// </remarks>
    /// <param name="model">The model instance to populate.</param>
    /// <param name="logger">Logger for warnings.</param>
    /// <param name="ps">Current runspace PowerShell, used for PowerShell class instantiation when needed.</param>
    private static void TryPopulateKrMultipartDerivedModel(KrMultipart model, Serilog.ILogger logger, PowerShell? ps)
    {
        try
        {
            // Only attempt to populate properties declared on the derived type.
            var t = model.GetType();
            if (t == typeof(KrMultipart))
            {
                return;
            }

            TryPopulateMultipartObjectProperties(model, model, t, logger, ps, depth: 0);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to populate KrMultipart-derived model properties for type {ParameterType}.", model.GetType());
        }
    }

    /// <summary>
    /// Populates KrPart-decorated properties declared on <paramref name="declaringType"/> from a multipart payload.
    /// </summary>
    /// <param name="target">The object instance to populate.</param>
    /// <param name="source">The multipart payload providing parts.</param>
    /// <param name="declaringType">The type whose declared properties should be considered.</param>
    /// <param name="logger">Logger for warnings.</param>
    /// <param name="ps">Current PowerShell runspace, used to instantiate PowerShell class types.</param>
    /// <param name="depth">Recursion depth for nested multipart binding.</param>
    private static void TryPopulateMultipartObjectProperties(
        object target,
        KrMultipart source,
        Type declaringType,
        Serilog.ILogger logger,
        PowerShell? ps,
        int depth)
    {
        if (depth > 4)
        {
            return;
        }

        // Build a case-insensitive map of public instance properties.
        // PowerShell-emitted classes can surface properties in ways that make DeclaringType/CanWrite checks unreliable.
        // We bind by part name (Content-Disposition: name="...") to property name.
        var props = declaringType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static p => p.GetIndexParameters().Length == 0)
            .Where(static p => !string.Equals(p.Name, "Parts", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in source.Parts
                     .Where(static p => !string.IsNullOrWhiteSpace(p.Name))
                     .GroupBy(p => p.Name!, StringComparer.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue(group.Key, out var prop))
            {
                continue;
            }

            // Avoid stomping values that were already set.
            try
            {
                var existing = prop.GetValue(target);
                if (existing is not null)
                {
                    continue;
                }
            }
            catch
            {
                // Ignore read failures; we can still attempt to set.
            }

            var matches = group.ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            var value = TryBuildValueForMultipartProperty(prop.PropertyType, matches, logger, ps, depth);
            if (value is null)
            {
                continue;
            }

            TrySetPropertyValue(target, prop, value, logger);
        }
    }

    /// <summary>
    /// Populates simple form-data properties (fields/files) on a model that does not inherit <see cref="KrFormData"/>.
    /// </summary>
    /// <param name="target">The object instance to populate.</param>
    /// <param name="source">The parsed form-data payload.</param>
    /// <param name="declaringType">The type whose declared properties should be considered.</param>
    /// <param name="logger">Logger for warnings.</param>
    private static void TryPopulateFormDataObjectProperties(
        object target,
        KrFormData source,
        Type declaringType,
        Serilog.ILogger logger)
    {
        var props = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in declaringType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(static p => p.GetIndexParameters().Length == 0))
        {
            // PowerShell-emitted classes can surface duplicate properties that differ only by case.
            // Keep the first occurrence and ignore duplicates to avoid CLS conflicts.
            if (!props.ContainsKey(prop.Name))
            {
                props.Add(prop.Name, prop);
            }
        }

        foreach (var kvp in source.Fields)
        {
            if (!props.TryGetValue(kvp.Key, out var prop))
            {
                continue;
            }

            var values = kvp.Value ?? [];
            object? value = null;
            if (prop.PropertyType == typeof(string))
            {
                value = values.FirstOrDefault();
            }
            else if (prop.PropertyType == typeof(string[]))
            {
                value = values;
            }
            else if (prop.PropertyType == typeof(object))
            {
                value = values.Length == 1 ? values[0] : values;
            }
            else if (prop.PropertyType.IsArray)
            {
                var elementType = prop.PropertyType.GetElementType();
                if (elementType is not null && (elementType == typeof(string) || elementType == typeof(object)))
                {
                    var array = Array.CreateInstance(elementType, values.Length);
                    for (var i = 0; i < values.Length; i++)
                    {
                        array.SetValue(values[i], i);
                    }
                    value = array;
                }
            }

            if (value is null)
            {
                continue;
            }

            TrySetPropertyValue(target, prop, value, logger);
        }

        foreach (var kvp in source.Files)
        {
            if (!props.TryGetValue(kvp.Key, out var prop))
            {
                continue;
            }

            var files = kvp.Value ?? [];
            object? value = null;
            if (prop.PropertyType == typeof(KrFilePart))
            {
                value = files.FirstOrDefault();
            }
            else if (prop.PropertyType == typeof(KrFilePart[]))
            {
                value = files;
            }
            else if (prop.PropertyType == typeof(object))
            {
                value = files.Length == 1 ? files[0] : files;
            }
            else if (prop.PropertyType.IsArray)
            {
                var elementType = prop.PropertyType.GetElementType();
                if (elementType is not null && (elementType == typeof(KrFilePart) || elementType == typeof(object)))
                {
                    var array = Array.CreateInstance(elementType, files.Length);
                    for (var i = 0; i < files.Length; i++)
                    {
                        array.SetValue(files[i], i);
                    }
                    value = array;
                }
            }

            if (value is null)
            {
                continue;
            }

            TrySetPropertyValue(target, prop, value, logger);
        }
    }

    /// <summary>
    /// Populates the raw multipart storage list (Parts) when the target exposes it.
    /// </summary>
    /// <param name="target">The object instance to populate.</param>
    /// <param name="source">The parsed multipart payload.</param>
    /// <param name="logger">Logger for warnings.</param>
    private static void TryPopulateMultipartStorage(object target, KrMultipart source, Serilog.ILogger logger)
    {
        if (target is KrMultipart typedTarget)
        {
            typedTarget.Parts.AddRange(source.Parts);
            return;
        }

        try
        {
            var prop = target.GetType().GetProperty(nameof(KrMultipart.Parts), BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(target) is IList list)
            {
                foreach (var part in source.Parts)
                {
                    _ = list.Add(part);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to populate multipart storage Parts for type {Type}.", target.GetType());
        }
    }

    /// <summary>
    /// Builds a value for a multipart model property from matching parts.
    /// </summary>
    private static object? TryBuildValueForMultipartProperty(
        Type propertyType,
        List<KrRawPart> matchingParts,
        Serilog.ILogger logger,
        PowerShell? ps,
        int depth)
    {
        if (propertyType.IsArray)
        {
            var elementType = propertyType.GetElementType();
            if (elementType is null)
            {
                return null;
            }

            var items = new List<object?>();
            foreach (var part in matchingParts)
            {
                items.Add(TryBuildSingleMultipartValue(elementType, part, logger, ps, depth));
            }

            var typed = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not null)
                {
                    typed.SetValue(items[i], i);
                }
            }

            return typed;
        }

        return TryBuildSingleMultipartValue(propertyType, matchingParts[0], logger, ps, depth);
    }

    /// <summary>
    /// Builds a single value for a multipart model property from one matching part.
    /// </summary>
    private static object? TryBuildSingleMultipartValue(
        Type targetType,
        KrRawPart part,
        Serilog.ILogger logger,
        PowerShell? ps,
        int depth)
    {
        if (targetType == typeof(string))
        {
            return TryReadPartAsString(part, logger);
        }

        if (part.NestedPayload is KrMultipart nested)
        {
            var nestedInstance = TryCreateObjectInstance(targetType, logger, ps);
            if (nestedInstance is null)
            {
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug(
                        "Multipart bind: failed to create nested instance for targetType={TargetType} partName={PartName} contentType={ContentType} runspaceId={RunspaceId}",
                        targetType,
                        part.Name,
                        part.ContentType,
                        ps?.Runspace?.InstanceId.ToString() ?? "<null>");
                }
                return null;
            }

            TryPopulateMultipartObjectProperties(nestedInstance, nested, targetType, logger, ps, depth + 1);
            return nestedInstance;
        }

        if (MediaTypeHelper.Canonicalize(part.ContentType).Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var json = TryReadPartAsString(part, logger);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            Hashtable? ht;
            try
            {
                ht = ConvertJsonToHashtable(json) as Hashtable;
            }
            catch (JsonException ex)
            {
                logger.Warning(ex, "Invalid JSON payload for multipart part '{PartName}'.", part.Name);
                throw new KrFormException("Invalid JSON payload for form part.", StatusCodes.Status400BadRequest);
            }

            if (ht is null)
            {
                return targetType == typeof(object) ? ConvertJsonToHashtable(json) : null;
            }

            // If the target is object-like, try to stash JSON into an AdditionalProperties bag.
            var obj = TryCreateObjectInstance(targetType, logger, ps);
            if (obj is not null)
            {
                if (TrySetAdditionalPropertiesBag(obj, ht, logger))
                {
                    return obj;
                }

                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug(
                        "Multipart bind: created instance for json part but could not set AdditionalProperties for targetType={TargetType} partName={PartName} runspaceId={RunspaceId}",
                        targetType,
                        part.Name,
                        ps?.Runspace?.InstanceId.ToString() ?? "<null>");
                }

                // If it isn't a bag model, fall back to returning the hashtable when possible.
            }

            if (obj is null && logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug(
                    "Multipart bind: failed to create instance for json part targetType={TargetType} partName={PartName} runspaceId={RunspaceId}",
                    targetType,
                    part.Name,
                    ps?.Runspace?.InstanceId.ToString() ?? "<null>");
            }

            return targetType == typeof(object) ? ht : null;
        }

        return null;
    }

    /// <summary>
    /// Reads a stored part payload as UTF-8 text.
    /// </summary>
    private static string? TryReadPartAsString(KrRawPart part, Serilog.ILogger logger)
    {
        try
        {
            return string.IsNullOrWhiteSpace(part.TempPath) || !File.Exists(part.TempPath)
                ? null
                : File.ReadAllText(part.TempPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to read multipart part payload from '{TempPath}'.", part.TempPath);
            return null;
        }
    }

    /// <summary>
    /// Attempts to create an instance of the requested type, using the current PowerShell runspace
    /// for PowerShell-emitted class types.
    /// </summary>
    private static object? TryCreateObjectInstance(Type type, Serilog.ILogger logger, PowerShell? ps)
    {
        // For PowerShell-emitted classes, create instances inside the current runspace only.
        // This avoids cross-runspace type identity mismatches.
        if (IsPowerShellClassType(type) && ps?.Runspace is not null)
        {
            var runspaceInstance = TryCreateObjectInstanceInRunspace(ps, type, logger);
            if (runspaceInstance is not null)
            {
                return runspaceInstance;
            }

            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Failed to create PowerShell class instance in the current runspace for type {Type}.", type);
            }

            // As a fallback, create an uninitialized instance so we can still populate properties.
            // This avoids returning null when runspace construction fails for PowerShell-emitted types.
            if (!type.IsValueType && !type.IsAbstract)
            {
                try
                {
#pragma warning disable SYSLIB0050
                    return FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                    {
                        logger.Debug(ex, "Failed to create uninitialized instance for PowerShell class type {Type}.", type);
                    }
                }
            }

            return null;
        }

        try
        {
            return Activator.CreateInstance(type, nonPublic: true);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug(ex, "Failed to create instance via Activator for type {Type}.", type);
            }
        }

        // PowerShell-emitted types sometimes fail Activator-based construction even though they are otherwise usable.
        // As a fallback, create an uninitialized instance so we can still populate properties.
        if (!type.IsValueType && !type.IsAbstract)
        {
            try
            {
#pragma warning disable SYSLIB0050
                return FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug(ex, "Failed to create uninitialized instance for type {Type}.", type);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an instance of <paramref name="type"/> using the current PowerShell runspace.
    /// </summary>
    private static object? TryCreateObjectInstanceInRunspace(PowerShell ps, Type type, Serilog.ILogger logger)
    {
        try
        {
            if (ps.Runspace is null)
            {
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug("Runspace instance creation skipped: PowerShell.Runspace is null for type {ParameterType}.", type);
                }
                return null;
            }

            using var localPs = PowerShell.Create();
            localPs.Runspace = ps.Runspace;

            var candidates = new[]
            {
                type.AssemblyQualifiedName,
                type.FullName,
                type.Name
            };

            foreach (var candidate in candidates.Where(static c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
            {
                localPs.Commands.Clear();
                localPs.Streams.ClearStreams();

                var escapedName = EscapePowerShellString(candidate!);
                _ = localPs.AddScript($"[Activator]::CreateInstance([type]'{escapedName}')");

                var results = localPs.Invoke();
                if (!localPs.HadErrors && results.Count > 0)
                {
                    return results[0]?.BaseObject;
                }

                // Keep trying other names; some PowerShell types resolve better via FullName/Name than AQN.
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug("Failed to create instance for type {ParameterType} using type name '{TypeName}'.", type, candidate);
                }
            }

            logger.Warning("Failed to resolve PowerShell class type {ParameterType} in the current runspace.", type);
            return null;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create PowerShell class instance for type {ParameterType}.", type);
            return null;
        }
    }

    /// <summary>
    /// Attempts to assign <paramref name="value"/> to the given property.
    /// </summary>
    private static void TrySetPropertyValue(object target, PropertyInfo prop, object value, Serilog.ILogger logger)
    {
        try
        {
            if (prop.PropertyType.IsInstanceOfType(value))
            {
                prop.SetValue(target, value);
                return;
            }

            // Allow assigning any value into [object].
            if (prop.PropertyType == typeof(object))
            {
                prop.SetValue(target, value);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set property {PropertyName} on type {Type}.", prop.Name, target.GetType());

            // Best-effort fallback for dynamically-emitted types: try writing the auto-property backing field.
            _ = TrySetPropertyBackingFieldValue(target, prop, value, logger);
        }
    }

    /// <summary>
    /// Attempts to set an auto-property backing field for a property on a target instance.
    /// </summary>
    private static bool TrySetPropertyBackingFieldValue(object target, PropertyInfo prop, object value, Serilog.ILogger logger)
    {
        try
        {
            var t = target.GetType();
            var field = t.GetField($"<{prop.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? t.GetField(prop.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field is null)
            {
                return false;
            }

            if (field.FieldType.IsInstanceOfType(value) || field.FieldType == typeof(object))
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to set backing field for property {PropertyName} on type {Type}.", prop.Name, target.GetType());
            return false;
        }
    }

    /// <summary>
    /// Attempts to set an <c>AdditionalProperties</c> bag (or similarly named) on a model instance.
    /// </summary>
    private static bool TrySetAdditionalPropertiesBag(object target, Hashtable ht, Serilog.ILogger logger)
    {
        try
        {
            ht = NormalizeAdditionalPropertiesBag(target, ht, logger);
            var prop = target.GetType().GetProperty("AdditionalProperties", BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
            {
                return TrySetAdditionalPropertiesBackingField(target, ht, logger);
            }

            if (prop.CanWrite && (prop.PropertyType == typeof(object) || prop.PropertyType.IsInstanceOfType(ht)))
            {
                try
                {
                    prop.SetValue(target, ht);
                    return true;
                }
                catch (Exception ex)
                {
                    // Some dynamically-emitted types have setters that can throw; fall back to backing field.
                    logger.Debug(ex, "Failed to set AdditionalProperties via setter on type {Type}; trying backing field.", target.GetType());
                    return TrySetAdditionalPropertiesBackingField(target, ht, logger);
                }
            }

            // Some PowerShell-emitted properties can be effectively read-only to reflection.
            return TrySetAdditionalPropertiesBackingField(target, ht, logger);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to set AdditionalProperties on type {Type}.", target.GetType());
            return false;
        }
    }

    /// <summary>
    /// Attempts to set an <c>AdditionalProperties</c> backing field on a model instance.
    /// </summary>
    /// <param name="target">The target instance.</param>
    /// <param name="ht">The hashtable to assign.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>True if the backing field was set; otherwise false.</returns>
    private static Hashtable NormalizeAdditionalPropertiesBag(object target, Hashtable ht, Serilog.ILogger logger)
    {
        if (!TryGetAdditionalPropertiesMetadata(target, out var additionalTypeName, out var patterns))
        {
            return ht;
        }
        // If there are pattern rules, apply those first.
        if (patterns.Count > 0)
        {
            return FilterPatternedAdditionalProperties(target, ht, patterns, logger);
        }
        // Otherwise, if there's a single type name, convert all properties to that type.
        return string.IsNullOrWhiteSpace(additionalTypeName)
            ? ht
            : ConvertAdditionalPropertiesByType(target, ht, additionalTypeName, logger);
    }

    /// <summary>
    /// Filters and converts additional properties based on pattern rules.
    /// </summary>
    /// <param name="target">The target instance.</param>
    /// <param name="ht">The original properties hashtable.</param>
    /// <param name="patterns">The pattern rules to apply.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The filtered and converted properties.</returns>
    private static Hashtable FilterPatternedAdditionalProperties(
        object target,
        Hashtable ht,
        IReadOnlyList<PatternPropertyRule> patterns,
        Serilog.ILogger logger)
    {
        var filtered = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in ht)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            var match = patterns.FirstOrDefault(p => p.IsMatch(key));
            if (match is null)
            {
                continue;
            }

            var targetType = ResolveAdditionalPropertiesType(target.GetType(), match.SchemaTypeName);
            filtered[key] = targetType is null
                ? entry.Value
                : ConvertAdditionalPropertyValue(entry.Value, targetType, logger);
        }

        return filtered;
    }

    /// <summary>
    /// Converts additional properties using a single resolved target type.
    /// </summary>
    /// <param name="target">The target instance.</param>
    /// <param name="ht">The original properties hashtable.</param>
    /// <param name="typeName">The additional properties type name.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The converted properties.</returns>
    private static Hashtable ConvertAdditionalPropertiesByType(
        object target,
        Hashtable ht,
        string typeName,
        Serilog.ILogger logger)
    {
        var targetType = ResolveAdditionalPropertiesType(target.GetType(), typeName);
        if (targetType is null)
        {
            return ht;
        }

        var converted = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in ht)
        {
            converted[entry.Key] = ConvertAdditionalPropertyValue(entry.Value, targetType, logger);
        }

        return converted;
    }

    private static object ConvertAdditionalPropertyValue(object? value, Type targetType, Serilog.ILogger logger)
    {
        if (value is null)
        {
            return value!;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(object))
        {
            return value;
        }

        if (value is Hashtable ht)
        {
            return ConvertHashtableToObject(ht, targetType, depth: 0, ps: null) ?? value;
        }

        if (targetType == typeof(string))
        {
            return value.ToString() ?? string.Empty;
        }

        if (targetType.IsEnum && value is string s)
        {
            try
            {
                return Enum.Parse(targetType, s, ignoreCase: true);
            }
            catch
            {
                return value;
            }
        }

        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture) ?? value;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to convert additional property to {TargetType}.", targetType);
            return value;
        }
    }

    private static bool TryGetAdditionalPropertiesMetadata(
        object target,
        out string? additionalTypeName,
        out List<PatternPropertyRule> patterns)
    {
        additionalTypeName = null;
        patterns = [];

        var metaProp = target.GetType().GetProperty(
            "AdditionalPropertiesMetadata",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (metaProp?.GetValue(null) is not Hashtable meta)
        {
            return false;
        }

        additionalTypeName = meta["AdditionalPropertiesType"] as string;

        if (meta["PatternProperties"] is IEnumerable list)
        {
            foreach (var entry in list)
            {
                if (entry is not Hashtable pattern)
                {
                    continue;
                }

                var keyPattern = pattern["KeyPattern"] as string;
                var schemaType = pattern["SchemaType"] as string;
                if (string.IsNullOrWhiteSpace(keyPattern))
                {
                    continue;
                }

                patterns.Add(new PatternPropertyRule(keyPattern, schemaType));
            }
        }

        return !string.IsNullOrWhiteSpace(additionalTypeName) || patterns.Count > 0;
    }

    /// <summary>
    /// Resolves a type for additional properties based on a type name or alias.
    /// </summary>
    /// <param name="targetType">The target type requesting the additional properties type.</param>
    /// <param name="typeName">The type name or alias to resolve.</param>
    /// <returns>The resolved type, or null if resolution failed.</returns>
    private static Type? ResolveAdditionalPropertiesType(Type targetType, string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var trimmed = typeName.Trim();

        return TryResolveAlias(trimmed)
            ?? TryResolveByTypeName(trimmed)
            ?? TryResolveInAssembly(targetType.Assembly, trimmed)
            ?? TryResolveInLoadedAssemblies(trimmed);
    }

    /// <summary>
    /// Attempts to resolve a known type alias (e.g., string, int32).
    /// </summary>
    /// <param name="typeName">The candidate alias.</param>
    /// <returns>The resolved CLR type, or null if no alias matched.</returns>
    private static Type? TryResolveAlias(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "string" => typeof(string),
            "int" or "int32" => typeof(int),
            "long" or "int64" => typeof(long),
            "double" => typeof(double),
            "float" => typeof(float),
            "decimal" => typeof(decimal),
            "bool" or "boolean" => typeof(bool),
            "object" => typeof(object),
            "hashtable" => typeof(Hashtable),
            _ => null
        };
    }

    /// <summary>
    /// Attempts to resolve a type using <see cref="Type.GetType(string, bool, bool)"/>.
    /// </summary>
    /// <param name="typeName">The fully qualified type name.</param>
    /// <returns>The resolved CLR type, or null when resolution failed.</returns>
    private static Type? TryResolveByTypeName(string typeName)
        => System.Type.GetType(typeName, throwOnError: false, ignoreCase: true);

    /// <summary>
    /// Attempts to resolve a type by searching within the provided assembly.
    /// </summary>
    /// <param name="assembly">The assembly to search.</param>
    /// <param name="typeName">The type name or full name to match.</param>
    /// <returns>The resolved CLR type, or null if no match exists.</returns>
    private static Type? TryResolveInAssembly(Assembly assembly, string typeName)
    {
        return assembly.GetTypes()
            .FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Attempts to resolve a type by searching all loaded assemblies.
    /// </summary>
    /// <param name="typeName">The type name or full name to match.</param>
    /// <returns>The resolved CLR type, or null if no match exists.</returns>
    private static Type? TryResolveInLoadedAssemblies(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var match = TryResolveInAssembly(asm, typeName);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to set a backing field for an <c>AdditionalProperties</c> auto-property.
    /// </summary>
    private static bool TrySetAdditionalPropertiesBackingField(object target, Hashtable ht, Serilog.ILogger logger)
    {
        try
        {
            var t = target.GetType();
            var field = t.GetField("<AdditionalProperties>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? t.GetField("AdditionalProperties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field is null)
            {
                return false;
            }

            if (field.FieldType == typeof(object) || field.FieldType.IsInstanceOfType(ht))
            {
                field.SetValue(target, ht);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Failed to set AdditionalProperties backing field on type {Type}.", target.GetType());
            return false;
        }
    }

    /// <summary>
    /// Returns true when the type is a PowerShell class emitted in a dynamic assembly.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True when the type originates from a PowerShell class assembly.</returns>
    private static bool IsPowerShellClassType(Type type)
    {
        var asm = type.Assembly;
        if (!asm.IsDynamic)
        {
            return false;
        }

        var fullName = asm.FullName;
        return fullName is not null && fullName.StartsWith("PowerShell Class Assembly", StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to create a <see cref="KrMultipart"/> instance for a derived parameter type.
    /// </summary>
    /// <param name="parameterType">The parameter type to construct.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <returns>A new multipart instance or null if creation failed.</returns>
    private static KrMultipart? TryCreateMultipartInstance(Type parameterType, Serilog.ILogger logger)
    {
        try
        {
            var instance = Activator.CreateInstance(parameterType, nonPublic: true) as KrMultipart;
            if (instance is null)
            {
                logger.Warning("Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            }
            return instance;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            return null;
        }
    }

    /// <summary>
    /// Attempts to create a <see cref="KrFormData"/> instance for a derived parameter type.
    /// </summary>
    /// <param name="parameterType">The parameter type to construct.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <returns>A new form data instance or null if creation failed.</returns>
    private static KrFormData? TryCreateFormDataInstance(Type parameterType, Serilog.ILogger logger)
    {
        try
        {
            var instance = Activator.CreateInstance(parameterType, nonPublic: true) as KrFormData;
            if (instance is null)
            {
                logger.Warning("Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            }
            return instance;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create form payload instance for parameter type {ParameterType}.", parameterType);
            return null;
        }
    }

    /// <summary>
    /// Attempts to create a derived <see cref="KrMultipart"/> instance using the current PowerShell runspace.
    /// </summary>
    /// <param name="ps">The current PowerShell instance.</param>
    /// <param name="parameterType">The parameter type to construct.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <returns>A new multipart instance or null if creation failed.</returns>
    private static KrMultipart? TryCreateMultipartInstanceInRunspace(PowerShell ps, Type parameterType, Serilog.ILogger logger)
    {
        try
        {
            using var localPs = PowerShell.Create();
            localPs.Runspace = ps.Runspace;

            var typeName = parameterType.FullName ?? parameterType.Name;
            var escapedName = EscapePowerShellString(typeName);
            _ = localPs.AddScript($"[Activator]::CreateInstance([type]'{escapedName}')");

            var results = localPs.Invoke();
            if (localPs.HadErrors || results.Count == 0)
            {
                logger.Warning("Failed to resolve PowerShell class type {ParameterType} in the current runspace.", parameterType);
                return null;
            }

            var instance = results[0]?.BaseObject as KrMultipart;
            if (instance is null)
            {
                logger.Warning("Resolved PowerShell class type {ParameterType} is not a KrMultipart instance.", parameterType);
            }

            return instance;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create PowerShell class instance for parameter type {ParameterType}.", parameterType);
            return null;
        }
    }

    /// <summary>
    /// Attempts to create a derived <see cref="KrFormData"/> instance using the current PowerShell runspace.
    /// </summary>
    /// <param name="ps">The current PowerShell instance.</param>
    /// <param name="parameterType">The parameter type to construct.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <returns>A new form data instance or null if creation failed.</returns>
    private static KrFormData? TryCreateFormDataInstanceInRunspace(PowerShell ps, Type parameterType, Serilog.ILogger logger)
    {
        try
        {
            using var localPs = PowerShell.Create();
            localPs.Runspace = ps.Runspace;

            var typeName = parameterType.FullName ?? parameterType.Name;
            var escapedName = EscapePowerShellString(typeName);
            _ = localPs.AddScript($"[Activator]::CreateInstance([type]'{escapedName}')");

            var results = localPs.Invoke();
            if (localPs.HadErrors || results.Count == 0)
            {
                logger.Warning("Failed to resolve PowerShell class type {ParameterType} in the current runspace.", parameterType);
                return null;
            }

            var instance = results[0]?.BaseObject as KrFormData;
            if (instance is null)
            {
                logger.Warning("Resolved PowerShell class type {ParameterType} is not a KrFormData instance.", parameterType);
            }

            return instance;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create PowerShell class instance for parameter type {ParameterType}.", parameterType);
            return null;
        }
    }

    /// <summary>
    /// Copies fields and files from a parsed form payload into a derived <see cref="KrFormData"/> instance.
    /// </summary>
    /// <param name="target">The instance to populate.</param>
    /// <param name="source">The parsed payload to copy from.</param>
    private static void CopyFormData(KrFormData target, KrFormData source)
    {
        foreach (var kvp in source.Fields)
        {
            target.Fields[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in source.Files)
        {
            target.Files[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Escapes a string for inclusion in a single-quoted PowerShell string literal.
    /// </summary>
    /// <param name="value">The raw string value.</param>
    /// <returns>An escaped string safe for single-quoted PowerShell literals.</returns>
    private static string EscapePowerShellString(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// Logs the injection of a parameter when debug logging is enabled.
    /// </summary>
    /// <param name="logger">The host logger.</param>
    /// <param name="param">The parameter being injected.</param>
    private static void LogInjectingParameter(Serilog.ILogger logger, ParameterForInjectionInfo param)
    {
        if (!logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            return;
        }

        var schemaType = param.Type?.ToString() ?? "<none>";
        var clrType = param.ParameterType?.FullName ?? "<unknown>";
        logger.Debug(
            "Injecting parameter '{Name}' schemaType='{SchemaType}' clrType='{ClrType}' from '{In}'.",
            param.Name,
            schemaType,
            clrType,
            param.In);
    }

    /// <summary>
    /// Gets the converted parameter value from the current request context.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="param">The parameter metadata.</param>
    /// <param name="shouldLog">Whether the value should be logged.</param>
    /// <returns>The converted parameter value.</returns>
    private static object? GetConvertedParameterValue(KestrunContext context, ParameterForInjectionInfo param, out bool shouldLog)
    {
        shouldLog = true;

        return context.Request.Form is not null && context.Request.HasFormContentType
            ? ConvertFormToValue(context.Request.Form, param)
            : GetParameterValueFromContext(context, param, out shouldLog);
    }

    /// <summary>
    /// Converts a request-body parameter from a raw string to a structured object when possible.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="param">The parameter metadata.</param>
    /// <param name="converted">The current converted value.</param>
    /// <returns>The updated value, possibly converted to an object/hashtable.</returns>
    private static object? ConvertBodyParameterIfNeeded(KestrunContext context, ParameterForInjectionInfo param, object? converted)
    {
        if (!ShouldConvertBody(param, converted))
        {
            return converted;
        }

        var rawString = (string)converted!;
        var bodyObj = TryConvertBodyByContentType(context, param, rawString);

        if (bodyObj is not null)
        {
            return bodyObj;
        }

        context.Logger.WarningSanitized(
            "Unable to convert body parameter '{Name}' with content types: {ContentTypes}. Using raw string value.",
            param.Name,
            param.ContentTypes);

        return converted;
    }

    /// <summary>
    /// Logs the addition of a parameter to the PowerShell invocation when requested and debug logging is enabled.
    /// </summary>
    /// <param name="logger">The host logger.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value to be added.</param>
    /// <param name="shouldLog">Whether logging should be performed.</param>
    private static void LogAddingParameter(Serilog.ILogger logger, string name, object? value, bool shouldLog)
    {
        if (!shouldLog || !logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            return;
        }

        var valueType = value?.GetType().FullName ?? "<null>";
        logger.DebugSanitized("Adding parameter '{Name}' ({ValueType}): {ConvertedValue}", name, valueType, value);
    }

    /// <summary>
    /// Stores the resolved parameter on the request context, either as the request body or a named parameter.
    /// </summary>
    /// <param name="context">The current Kestrun context.</param>
    /// <param name="param">The parameter metadata.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The resolved value.</param>
    private static void StoreResolvedParameter(KestrunContext context, ParameterForInjectionInfo param, string name, object? value)
    {
        var resolved = new ParameterForInjectionResolved(param, value);
        if (param.IsRequestBody)
        {
            context.Parameters.Body = resolved;
            return;
        }

        context.Parameters.Parameters[name] = resolved;
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
        var isSingleContentType = param.ContentTypes.Count == 1;

        var requestMediaType = MediaTypeHelper.Canonicalize(context.Request.ContentType);

        if (string.IsNullOrEmpty(requestMediaType))
        {
            if (!isSingleContentType)
            {
                throw new InvalidOperationException(
                    "Content-Type header is missing; cannot convert body to object.");
            }

            var inferred = MediaTypeHelper.Canonicalize(param.ContentTypes[0]);
            return ConvertByCanonicalMediaType(inferred, context, rawBodyString, param);
        }

        return ConvertByCanonicalMediaType(requestMediaType, context, rawBodyString, param);
    }

    /// <summary>
    /// Converts the body string to an object based on the canonical media type.
    /// </summary>
    /// <param name="canonicalMediaType">   The canonical media type of the request body.</param>
    /// <param name="context"> The current Kestrun context.</param>
    /// <param name="rawBodyString"> The raw body string from the request.</param>
    /// <param name="param">The parameter information.</param>
    /// <returns> The converted body object.</returns>
    private static object? ConvertByCanonicalMediaType(
        string canonicalMediaType,
        KestrunContext context,
        string rawBodyString,
        ParameterForInjectionInfo param)
    {
        return canonicalMediaType switch
        {
            "application/json" => ConvertJsonToHashtable(rawBodyString),
            "application/yaml" => ConvertYamlToHashtable(rawBodyString),
            "application/xml" => ConvertXmlBodyToParameterType(rawBodyString, param.ParameterType),
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

    private const int MaxObjectBindingDepth = 32;

    private static object? ConvertXmlBodyToParameterType(string xml, Type parameterType)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XElement root;
        try
        {
            // Clients often include an XML declaration with an encoding (e.g. UTF-8). When parsing from a .NET
            // string (already decoded), some parsers can reject mismatched/pointless encoding declarations.
            // Strip the declaration if present.
            var cleaned = xml.TrimStart('\uFEFF', '\u200B', '\u0000', ' ', '\t', '\r', '\n');
            if (cleaned.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                var endDecl = cleaned.IndexOf("?>", StringComparison.Ordinal);
                if (endDecl >= 0)
                {
                    cleaned = cleaned[(endDecl + 2)..].TrimStart();
                }
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(cleaned);
            }
            catch
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                };

                using var reader = XmlReader.Create(new StringReader(cleaned), settings);
                doc = XDocument.Load(reader);
            }

            root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");
        }
        catch
        {
            return null;
        }

        // If the parameter expects a string, don't attempt to parse.
        if (parameterType == typeof(string))
        {
            return xml;
        }

        var xmlMetadata = XmlHelper.GetOpenApiXmlMetadataForType(parameterType);
        var wrapped = XmlHelper.ToHashtable(root, xmlMetadata);

        // Normalize from XmlHelper's { RootName = { ... } } shape into the element map itself.
        var rootMap = ExtractRootMapForBinding(wrapped, root.Name.LocalName);
        if (rootMap is null)
        {
            return null;
        }

        NormalizeWrappedArrays(rootMap, xmlMetadata);

        // For PowerShell script classes, the runtime may produce a new (dynamic) type in the request runspace.
        // Creating an instance here (using a Type captured during route registration) can produce a type-identity
        // mismatch at parameter-binding time ("cannot convert Product to Product").
        // Returning a hashtable lets PowerShell perform the conversion to the *current* runspace type.
        if (parameterType == typeof(object) || typeof(IDictionary).IsAssignableFrom(parameterType) || parameterType.Assembly.IsDynamic)
        {
            return rootMap;
        }
        // Otherwise, attempt to convert to the target type.
        return ConvertHashtableToObject(rootMap, parameterType, depth: 0, ps: null);
    }

    private static Hashtable? ExtractRootMapForBinding(Hashtable wrapped, string rootLocalName)
    {
        // XmlHelper.ToHashtable returns { rootName = childMap } plus any mapped attributes at the same level.
        if (!TryGetHashtableValue(wrapped, rootLocalName, out var rootObj))
        {
            // Fallback: if there's exactly one entry and it's a hashtable, use it.
            if (wrapped.Count == 1)
            {
                var only = wrapped.Values.Cast<object?>().FirstOrDefault();
                return only as Hashtable;
            }
            return wrapped;
        }

        if (rootObj is not Hashtable rootMap)
        {
            return null;
        }

        // Merge any sibling keys (e.g., metadata-guided attributes) into the root map.
        foreach (DictionaryEntry entry in wrapped)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            if (string.Equals(key, rootLocalName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rootMap[key] = entry.Value;
        }

        return rootMap;
    }

    /// <summary>
    /// Normalizes wrapped arrays in the root map based on XML metadata.
    /// </summary>
    /// <param name="rootMap">The root hashtable map.</param>
    /// <param name="xmlMetadata">The XML metadata hashtable.</param>
    private static void NormalizeWrappedArrays(Hashtable rootMap, Hashtable? xmlMetadata)
    {
        if (!TryGetXmlMetadataProperties(xmlMetadata, out var propsHash))
        {
            return;
        }

        foreach (DictionaryEntry entry in propsHash)
        {
            if (!TryGetWrappedArrayMetadata(entry, out var propertyName, out var xmlName))
            {
                continue;
            }

            if (!TryGetWrapperHashtable(rootMap, propertyName, xmlName, out var wrapper))
            {
                continue;
            }

            var unwrapped = TryUnwrapWrapper(wrapper);
            if (unwrapped is not null)
            {
                rootMap[propertyName] = unwrapped;
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve the <c>Properties</c> hashtable from XML metadata.
    /// </summary>
    /// <param name="xmlMetadata">XML metadata hashtable.</param>
    /// <param name="properties">The properties hashtable when present.</param>
    /// <returns><c>true</c> when the properties hashtable exists; otherwise <c>false</c>.</returns>
    private static bool TryGetXmlMetadataProperties(Hashtable? xmlMetadata, out Hashtable properties)
    {
        if (xmlMetadata?["Properties"] is Hashtable propsHash)
        {
            properties = propsHash;
            return true;
        }

        properties = default!;
        return false;
    }

    /// <summary>
    /// Extracts metadata for a wrapped array property from a single <see cref="DictionaryEntry"/>.
    /// </summary>
    /// <param name="entry">Entry from <c>xmlMetadata.Properties</c>.</param>
    /// <param name="propertyName">The CLR property name.</param>
    /// <param name="xmlName">The XML element name (or the CLR name when not overridden).</param>
    /// <returns><c>true</c> when the entry describes a wrapped property; otherwise <c>false</c>.</returns>
    private static bool TryGetWrappedArrayMetadata(DictionaryEntry entry, out string propertyName, out string xmlName)
    {
        if (entry.Key is not string propName || entry.Value is not Hashtable propMeta)
        {
            propertyName = default!;
            xmlName = default!;
            return false;
        }

        if (propMeta["Wrapped"] is not bool wrapped || !wrapped)
        {
            propertyName = default!;
            xmlName = default!;
            return false;
        }

        propertyName = propName;
        xmlName = propMeta["Name"] as string ?? propName;
        return true;
    }

    /// <summary>
    /// Attempts to find a wrapper hashtable for a wrapped array property in the root map.
    /// </summary>
    /// <param name="rootMap">The root map produced by XML parsing.</param>
    /// <param name="propertyName">CLR property name to search.</param>
    /// <param name="xmlName">XML element name to search (fallback).</param>
    /// <param name="wrapper">The wrapper hashtable if found.</param>
    /// <returns><c>true</c> when a wrapper hashtable is found; otherwise <c>false</c>.</returns>
    private static bool TryGetWrapperHashtable(Hashtable rootMap, string propertyName, string xmlName, out Hashtable wrapper)
    {
        if (!TryGetHashtableValue(rootMap, propertyName, out var raw)
            && !TryGetHashtableValue(rootMap, xmlName, out raw))
        {
            wrapper = default!;
            return false;
        }

        if (raw is Hashtable wrapperHash)
        {
            wrapper = wrapperHash;
            return true;
        }

        wrapper = default!;
        return false;
    }

    /// <summary>
    /// Unwraps a wrapper hashtable into an item list/value when possible.
    /// </summary>
    /// <param name="wrapper">Wrapper hashtable.</param>
    /// <returns>The unwrapped value, or <c>null</c> if it cannot be unwrapped.</returns>
    private static object? TryUnwrapWrapper(Hashtable wrapper)
    {
        return TryGetHashtableValue(wrapper, "Item", out var itemValue)
            ? itemValue
            : wrapper.Count == 1
                ? wrapper.Values.Cast<object?>().FirstOrDefault()
                : null;
    }

    /// <summary>
    /// Converts a hashtable into an object of the specified target type.
    /// </summary>
    /// <param name="data">The hashtable data to convert.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <param name="ps">The current PowerShell instance, if available.</param>
    /// <returns>The converted object, or null if conversion is not possible.</returns>
    private static object? ConvertHashtableToObject(Hashtable data, Type targetType, int depth, PowerShell? ps)
    {
        if (depth >= MaxObjectBindingDepth)
        {
            return null;
        }

        var instance = TryCreateObjectInstance(targetType, Serilog.Log.Logger, ps);
        if (instance is null)
        {
            return null;
        }

        PopulateObjectFromHashtable(instance, targetType, data, depth, Serilog.Log.Logger, ps);
        return instance;
    }

    /// <summary>
    /// Attempts to convert a hashtable into an instance of the specified parameter type.
    /// </summary>
    /// <param name="parameterType">The target parameter type.</param>
    /// <param name="data">The hashtable data to convert.</param>
    /// <param name="logger">The logger to use for diagnostic warnings.</param>
    /// <param name="ps">The current PowerShell instance, if available.</param>
    /// <returns>The converted object, or null if conversion is not possible.</returns>
    private static object? TryConvertHashtableToParameterType(
        Type parameterType,
        Hashtable data,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        if (parameterType == typeof(object) || typeof(IDictionary).IsAssignableFrom(parameterType))
        {
            return null;
        }

        if (!parameterType.IsClass)
        {
            return null;
        }

        var instance = TryCreateObjectInstance(parameterType, logger, ps);
        if (instance is null)
        {
            return null;
        }

        PopulateObjectFromHashtable(instance, instance.GetType(), data, depth: 0, logger, ps);
        return instance;
    }

    private static void PopulateObjectFromHashtable(
        object instance,
        Type targetType,
        Hashtable data,
        int depth,
        Serilog.ILogger logger,
        PowerShell? ps)
    {
        var props = targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod is not null)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var fields = targetType
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        var extras = new Hashtable(StringComparer.OrdinalIgnoreCase);

        foreach (DictionaryEntry entry in data)
        {
            if (entry.Key is not string rawKey)
            {
                continue;
            }

            var key = rawKey.StartsWith('@') ? rawKey[1..] : rawKey;

            if (props.TryGetValue(key, out var prop))
            {
                var converted = ConvertToTargetType(entry.Value, prop.PropertyType, depth + 1, ps);
                prop.SetValue(instance, converted);
                continue;
            }

            if (fields.TryGetValue(key, out var field))
            {
                var converted = ConvertToTargetType(entry.Value, field.FieldType, depth + 1, ps);
                field.SetValue(instance, converted);
                continue;
            }

            extras[key] = entry.Value;
        }

        if (extras.Count > 0)
        {
            if (TryGetHashtableValue(data, "AdditionalProperties", out var existingBag)
                && existingBag is Hashtable existingHash)
            {
                foreach (DictionaryEntry entry in extras)
                {
                    existingHash[entry.Key] = entry.Value;
                }

                _ = TrySetAdditionalPropertiesBag(instance, existingHash, logger);
            }
            else
            {
                _ = TrySetAdditionalPropertiesBag(instance, extras, logger);
            }
        }

        return;
    }

    /// <summary>
    /// Converts a value to the specified target type, handling complex objects and collections.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <param name="ps">The current PowerShell instance, if available.</param>
    /// <returns>The converted value, or null if conversion is not possible.</returns>
    private static object? ConvertToTargetType(object? value, Type targetType, int depth, PowerShell? ps)
    {
        if (value is null)
        {
            return null;
        }

        targetType = UnwrapNullableTargetType(targetType);

        return targetType.IsInstanceOfType(value)
            ? value
            : TryConvertHashtableValue(value, targetType, depth, ps, out var convertedFromHashtable)
                ? convertedFromHashtable
                : TryConvertListOrArrayValue(value, targetType, depth, ps, out var convertedFromEnumerable)
                    ? convertedFromEnumerable
                    : ConvertScalarValue(value, targetType);
    }

    /// <summary>
    /// Unwraps a nullable target type to its underlying non-nullable type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <returns>The underlying non-nullable type, or the original type when not nullable.</returns>
    private static Type UnwrapNullableTargetType(Type targetType)
        => Nullable.GetUnderlyingType(targetType) ?? targetType;

    /// <summary>
    /// Converts a hashtable value into the target type when applicable.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <param name="ps">The current PowerShell instance, if available.</param>
    /// <param name="converted">Converted result.</param>
    /// <returns><c>true</c> when the value was handled; otherwise <c>false</c>.</returns>
    private static bool TryConvertHashtableValue(object value, Type targetType, int depth, PowerShell? ps, out object? converted)
    {
        if (value is not Hashtable ht)
        {
            converted = null;
            return false;
        }

        converted = typeof(IDictionary).IsAssignableFrom(targetType)
            ? ht
            : ConvertHashtableToObject(ht, targetType, depth, ps);
        return true;
    }

    /// <summary>
    /// Converts list/array values into the target type when applicable.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="depth">Current recursion depth.</param>
    /// <param name="ps">The current PowerShell instance, if available.</param>
    /// <param name="converted">Converted result.</param>
    /// <returns><c>true</c> when the value was handled; otherwise <c>false</c>.</returns>
    private static bool TryConvertListOrArrayValue(object value, Type targetType, int depth, PowerShell? ps, out object? converted)
    {
        if (value is List<object?> list)
        {
            converted = ConvertEnumerableToTargetType(list, targetType, depth, ps);
            return true;
        }

        if (value is object?[] arr)
        {
            converted = ConvertEnumerableToTargetType(arr, targetType, depth, ps);
            return true;
        }

        converted = null;
        return false;
    }

    /// <summary>
    /// Converts a scalar (non-hashtable, non-collection) value into the target type.
    /// </summary>
    /// <param name="value">Scalar value to convert.</param>
    /// <param name="targetType">Target type.</param>
    /// <returns>The converted value, or <c>null</c> when conversion fails.</returns>
    private static object? ConvertScalarValue(object value, Type targetType)
    {
        var str = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);

        return TryConvertScalarByType(str, targetType, out var converted)
            ? converted
            : TryChangeType(value, targetType);
    }

    /// <summary>
    /// Attempts to convert a scalar string representation to common primitive target types.
    /// </summary>
    /// <param name="str">String representation of the value.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="converted">Converted result.</param>
    /// <returns><c>true</c> when converted; otherwise <c>false</c>.</returns>
    private static bool TryConvertScalarByType(string? str, Type targetType, out object? converted)
    {
        if (TryConvertPrimitiveScalar(str, targetType, out converted))
        {
            return true;
        }

        if (targetType.IsEnum)
        {
            converted = TryParseEnum(targetType, str);
            return converted is not null;
        }

        converted = null;
        return false;
    }

    /// <summary>
    /// Attempts to convert a scalar string representation into a primitive CLR type.
    /// </summary>
    /// <param name="str">String representation of the value.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="converted">Converted result.</param>
    /// <returns><c>true</c> when converted; otherwise <c>false</c>.</returns>
    private static bool TryConvertPrimitiveScalar(string? str, Type targetType, out object? converted)
    {
        switch (System.Type.GetTypeCode(targetType))
        {
            case TypeCode.String:
                converted = str;
                return true;
            case TypeCode.Int32:
                converted = TryParseInt32(str);
                return converted is not null;
            case TypeCode.Int64:
                converted = TryParseInt64(str);
                return converted is not null;
            case TypeCode.Double:
                converted = TryParseDouble(str);
                return converted is not null;
            case TypeCode.Decimal:
                converted = TryParseDecimal(str);
                return converted is not null;
            case TypeCode.Boolean:
                converted = TryParseBoolean(str);
                return converted is not null;
            default:
                converted = null;
                return false;
        }
    }

    /// <summary>
    /// Attempts to parse an <see cref="int"/> from a string.
    /// </summary>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed value, or <c>null</c> when parsing fails.</returns>
    private static int? TryParseInt32(string? str)
        => int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    /// <summary>
    /// Attempts to parse a <see cref="long"/> from a string.
    /// </summary>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed value, or <c>null</c> when parsing fails.</returns>
    private static long? TryParseInt64(string? str)
        => long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;

    /// <summary>
    /// Attempts to parse a <see cref="double"/> from a string.
    /// </summary>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed value, or <c>null</c> when parsing fails.</returns>
    private static double? TryParseDouble(string? str)
        => double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>
    /// Attempts to parse a <see cref="decimal"/> from a string.
    /// </summary>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed value, or <c>null</c> when parsing fails.</returns>
    private static decimal? TryParseDecimal(string? str)
        => decimal.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) ? dec : null;

    /// <summary>
    /// Attempts to parse a <see cref="bool"/> from a string.
    /// </summary>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed value, or <c>null</c> when parsing fails.</returns>
    private static bool? TryParseBoolean(string? str)
        => bool.TryParse(str, out var b) ? b : null;

    /// <summary>
    /// Attempts to parse an enum value from a string.
    /// </summary>
    /// <param name="targetType">Enum type.</param>
    /// <param name="str">String representation.</param>
    /// <returns>The parsed enum value, or <c>null</c> when parsing fails.</returns>
    private static object? TryParseEnum(Type targetType, string? str)
    {
        if (str is null)
        {
            return null;
        }

        try
        {
            return Enum.Parse(targetType, str, ignoreCase: true);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts a generic scalar conversion via <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <param name="targetType">Target type.</param>
    /// <returns>The converted value, or <c>null</c> when conversion fails.</returns>
    private static object? TryChangeType(object value, Type targetType)
    {
        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertEnumerableToTargetType(IEnumerable enumerable, Type targetType, int depth, PowerShell? ps)
    {
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType() ?? typeof(object);
            var items = new List<object?>();
            foreach (var item in enumerable)
            {
                items.Add(ConvertToTargetType(item, elementType, depth + 1, ps));
            }

            var arr = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                arr.SetValue(items[i], i);
            }

            return arr;
        }

        // Default to List<T> for generic IEnumerable targets.
        if (targetType.IsGenericType)
        {
            var genDef = targetType.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var item in enumerable)
                {
                    _ = list.Add(ConvertToTargetType(item, elementType, depth + 1, ps));
                }
                return list;
            }
        }

        return null;
    }

    private static bool TryGetHashtableValue(Hashtable table, string key, out object? value)
    {
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is string s && string.Equals(s, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
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
        return Encoding.UTF8.GetBytes(trimmed);
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
            bytes[i] = byte.Parse(s.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return true;
    }
}
