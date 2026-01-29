using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;
using Logger = Serilog.ILogger;

namespace Kestrun.Forms;

/// <summary>
/// Parses incoming form payloads into normalized form payloads.
/// </summary>
public static class KrFormParser
{
    /// <summary>
    /// Parses the incoming request into a normalized form payload.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed payload.</returns>
    public static async Task<KrFormPayload> ParseAsync(HttpContext context, KrFormOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var logger = options.Logger
            ?? context.RequestServices.GetService(typeof(Serilog.ILogger)) as Serilog.ILogger
            ?? Log.Logger;
        using var _ = logger.BeginTimedOperation("KrFormParser.ParseAsync");

        var contentTypeHeader = context.Request.ContentType;
        var contentEncoding = context.Request.Headers[HeaderNames.ContentEncoding].ToString();
        var requestDecompressionEnabled = DetectRequestDecompressionEnabled(context);

        logger.Information(
            "Form route start: Content-Type={ContentType}, Content-Encoding={ContentEncoding}, RequestDecompressionEnabled={RequestDecompressionEnabled}",
            contentTypeHeader,
            string.IsNullOrWhiteSpace(contentEncoding) ? "<none>" : contentEncoding,
            requestDecompressionEnabled);

        if (string.IsNullOrWhiteSpace(contentTypeHeader))
        {
            logger.Error("Missing Content-Type header for form parsing.");
            throw new KrFormException("Content-Type header is required for form parsing.", StatusCodes.Status415UnsupportedMediaType);
        }

        if (!MediaTypeHeaderValue.TryParse(contentTypeHeader, out var mediaType))
        {
            logger.Error("Invalid Content-Type header: {ContentType}", contentTypeHeader);
            throw new KrFormException("Invalid Content-Type header.", StatusCodes.Status415UnsupportedMediaType);
        }

        var normalizedMediaType = mediaType.MediaType.Value ?? string.Empty;
        if (!IsAllowedRequestContentType(normalizedMediaType, options.AllowedRequestContentTypes))
        {
            if (options.RejectUnknownRequestContentType)
            {
                logger.Error("Rejected request Content-Type: {ContentType}", normalizedMediaType);
                throw new KrFormException("Unsupported Content-Type for form parsing.", StatusCodes.Status415UnsupportedMediaType);
            }

            logger.Warning("Unknown Content-Type allowed: {ContentType}", normalizedMediaType);
        }

        if (IsMultipartContentType(normalizedMediaType) && !mediaType.Boundary.HasValue)
        {
            logger.Error("Missing multipart boundary for Content-Type: {ContentType}", normalizedMediaType);
            throw new KrFormException("Missing multipart boundary.", StatusCodes.Status400BadRequest);
        }

        ApplyRequestBodyLimit(context, options, logger);

        return normalizedMediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            ? await ParseUrlEncodedAsync(context, options, logger, cancellationToken).ConfigureAwait(false)
            : normalizedMediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
                ? await ParseMultipartFormDataAsync(context, mediaType, options, logger, cancellationToken).ConfigureAwait(false)
                : normalizedMediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)
                    ? await ParseMultipartOrderedAsync(context, mediaType, options, logger, 0, cancellationToken).ConfigureAwait(false)
                    : throw new KrFormException("Unsupported Content-Type for form parsing.", StatusCodes.Status415UnsupportedMediaType);
    }

    /// <summary>
    /// Parses the incoming request into a normalized form payload. Synchronous wrapper.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed payload.</returns>
    public static KrFormPayload Parse(HttpContext context, KrFormOptions options, CancellationToken cancellationToken) =>
           ParseAsync(context, options, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Applies the request body size limit based on the provided options.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <param name="options">The form parsing options containing limits.</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    private static void ApplyRequestBodyLimit(HttpContext context, KrFormOptions options, Logger logger)
    {
        if (!options.Limits.MaxRequestBodyBytes.HasValue)
        {
            return;
        }

        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature == null || feature.IsReadOnly)
        {
            logger.Debug("Request body size feature not available or read-only.");
            return;
        }

        feature.MaxRequestBodySize = options.Limits.MaxRequestBodyBytes;
        logger.Debug("Set MaxRequestBodySize to {MaxBytes}", options.Limits.MaxRequestBodyBytes);
    }

    private static async Task<KrFormPayload> ParseUrlEncodedAsync(HttpContext context, KrFormOptions options, Logger logger, CancellationToken cancellationToken)
    {
        var payload = new KrFormData();
        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        foreach (var key in form.Keys)
        {
            payload.Fields[key] = [.. form[key].Select(static v => v ?? string.Empty)];
        }

        var rules = CreateRuleMap(options);
        ValidateRequiredRules(payload, rules, logger);

        logger.Information("Parsed x-www-form-urlencoded payload with {FieldCount} fields.", payload.Fields.Count);
        return payload;
    }

    private static async Task<KrFormPayload> ParseMultipartFormDataAsync(HttpContext context, MediaTypeHeaderValue mediaType, KrFormOptions options, Logger logger, CancellationToken cancellationToken)
    {
        var boundary = GetBoundary(mediaType);
        var reader = new MultipartReader(boundary, context.Request.Body)
        {
            HeadersLengthLimit = options.Limits.MaxHeaderBytesPerPart
        };

        var payload = new KrFormData();
        var rules = CreateRuleMap(options);
        var partIndex = 0;
        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            partIndex++;
            if (partIndex > options.Limits.MaxParts)
            {
                logger.Error("Multipart form exceeded MaxParts limit ({MaxParts}).", options.Limits.MaxParts);
                throw new KrFormLimitExceededException("Too many multipart sections.");
            }

            var headers = ToHeaderDictionary(section.Headers ?? []);
            var (name, fileName, contentDisposition) = GetContentDisposition(section, logger);
            var contentType = section.ContentType ?? (string.IsNullOrWhiteSpace(fileName) ? "text/plain" : "application/octet-stream");
            var contentEncoding = GetHeaderValue(headers, HeaderNames.ContentEncoding);
            var declaredLength = GetHeaderLong(headers, HeaderNames.ContentLength);

            var rule = name != null && rules.TryGetValue(name, out var match) ? match : null;
            var partContext = new KrPartContext
            {
                Index = partIndex - 1,
                Name = name,
                FileName = fileName,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                DeclaredLength = declaredLength,
                Headers = headers,
                Rule = rule
            };

            if (logger.IsEnabled(LogEventLevel.Debug))
            {
                logger.Debug("Multipart part {Index} name={Name} filename={FileName} contentType={ContentType} contentEncoding={ContentEncoding} declaredLength={DeclaredLength}",
                    partIndex - 1,
                    name,
                    fileName,
                    contentType,
                    string.IsNullOrWhiteSpace(contentEncoding) ? "<none>" : contentEncoding,
                    declaredLength);
            }

            var action = await InvokeOnPartAsync(options, partContext, logger).ConfigureAwait(false);
            if (action == KrPartAction.Reject)
            {
                logger.Error("Part rejected by hook: {PartIndex}", partIndex - 1);
                throw new KrFormException("Part rejected by policy.", StatusCodes.Status400BadRequest);
            }

            if (action == KrPartAction.Skip)
            {
                logger.Warning("Part skipped by hook: {PartIndex}", partIndex - 1);
                await DrainSectionAsync(section.Body, options, contentEncoding, logger, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                ValidateFilePart(name, fileName, contentType, rule, payload, logger);
                var result = await StorePartAsync(section.Body, options, rule, fileName, contentEncoding, logger, cancellationToken).ConfigureAwait(false);
                totalBytes += result.Length;

                var filePart = new KrFilePart
                {
                    Name = name!,
                    OriginalFileName = fileName,
                    ContentType = contentType,
                    Length = result.Length,
                    TempPath = result.TempPath,
                    Sha256 = result.Sha256,
                    Headers = headers
                };

                AppendFile(payload.Files, filePart, rule, logger);
                if (string.IsNullOrWhiteSpace(result.TempPath))
                {
                    logger.Warning("File part {Index} name={Name} was not stored to disk (bytes={Bytes}).", partIndex - 1, name, result.Length);
                }
                else
                {
                    logger.Information("Stored file part {Index} name={Name} filename={FileName} contentType={ContentType} bytes={Bytes}",
                        partIndex - 1, name, fileName, contentType, result.Length);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    logger.Error("Field part missing name.");
                    throw new KrFormException("Field part must include a name.", StatusCodes.Status400BadRequest);
                }
                var value = await ReadFieldValueAsync(section.Body, options, contentEncoding, logger, cancellationToken).ConfigureAwait(false);
                AppendField(payload.Fields, name ?? string.Empty, value);
                totalBytes += Encoding.UTF8.GetByteCount(value);
                logger.Debug("Parsed field part {Index} name={Name} bytes={Bytes}", partIndex - 1, name, Encoding.UTF8.GetByteCount(value));
            }
        }

        ValidateRequiredRules(payload, rules, logger);
        stopwatch.Stop();
        logger.Information("Parsed multipart/form-data with {Parts} parts, {Files} files, {Bytes} bytes in {ElapsedMs} ms.",
            partIndex, payload.Files.Sum(k => k.Value.Length), totalBytes, stopwatch.ElapsedMilliseconds);

        return payload;
    }

    private static async Task<KrFormPayload> ParseMultipartOrderedAsync(HttpContext context, MediaTypeHeaderValue mediaType, KrFormOptions options, Logger logger, int nestingDepth, CancellationToken cancellationToken)
    {
        var boundary = GetBoundary(mediaType);
        return await ParseMultipartFromStreamAsync(context.Request.Body, boundary, options, logger, nestingDepth, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<KrFormPayload> ParseMultipartFromStreamAsync(Stream body, string boundary, KrFormOptions options, Logger logger, int nestingDepth, CancellationToken cancellationToken)
    {
        var reader = new MultipartReader(boundary, body)
        {
            HeadersLengthLimit = options.Limits.MaxHeaderBytesPerPart
        };

        var payload = new KrMultipart();
        var rules = CreateRuleMap(options);
        var partIndex = 0;
        long totalBytes = 0;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            partIndex++;
            if (partIndex > options.Limits.MaxParts)
            {
                logger.Error("Multipart payload exceeded MaxParts limit ({MaxParts}).", options.Limits.MaxParts);
                throw new KrFormLimitExceededException("Too many multipart sections.");
            }

            var headers = ToHeaderDictionary(section.Headers ?? []);
            var contentType = section.ContentType ?? "application/octet-stream";
            var allowMissingDisposition = IsMultipartContentType(contentType);
            var (name, fileName, _) = GetContentDisposition(section, logger, allowMissing: allowMissingDisposition);
            var contentEncoding = GetHeaderValue(headers, HeaderNames.ContentEncoding);
            var declaredLength = GetHeaderLong(headers, HeaderNames.ContentLength);

            var rule = name != null && rules.TryGetValue(name, out var match) ? match : null;
            var partContext = new KrPartContext
            {
                Index = partIndex - 1,
                Name = name,
                FileName = fileName,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                DeclaredLength = declaredLength,
                Headers = headers,
                Rule = rule
            };

            if (logger.IsEnabled(LogEventLevel.Debug))
            {
                logger.Debug("Ordered part {Index} name={Name} filename={FileName} contentType={ContentType} contentEncoding={ContentEncoding} declaredLength={DeclaredLength}",
                    partIndex - 1,
                    name,
                    fileName,
                    contentType,
                    string.IsNullOrWhiteSpace(contentEncoding) ? "<none>" : contentEncoding,
                    declaredLength);
            }

            var action = await InvokeOnPartAsync(options, partContext, logger).ConfigureAwait(false);
            if (action == KrPartAction.Reject)
            {
                logger.Error("Ordered part rejected by hook: {PartIndex}", partIndex - 1);
                throw new KrFormException("Part rejected by policy.", StatusCodes.Status400BadRequest);
            }

            if (action == KrPartAction.Skip)
            {
                logger.Warning("Ordered part skipped by hook: {PartIndex}", partIndex - 1);
                await DrainSectionAsync(section.Body, options, contentEncoding, logger, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var result = await StorePartAsync(section.Body, options, rule, null, contentEncoding, logger, cancellationToken).ConfigureAwait(false);
            totalBytes += result.Length;

            KrFormPayload? nested = null;
            if (IsMultipartContentType(contentType))
            {
                if (nestingDepth >= options.Limits.MaxNestingDepth)
                {
                    logger.Error("Nested multipart depth exceeded limit {MaxDepth}.", options.Limits.MaxNestingDepth);
                    throw new KrFormLimitExceededException("Nested multipart depth exceeded.");
                }

                if (TryGetBoundary(contentType, out var nestedBoundary))
                {
                    if (!string.IsNullOrWhiteSpace(result.TempPath))
                    {
                        await using var nestedStream = File.OpenRead(result.TempPath);
                        nested = await ParseMultipartFromStreamAsync(nestedStream, nestedBoundary, options, logger, nestingDepth + 1, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.Warning("Nested multipart part was not stored to disk; skipping nested parse.");
                    }
                }
                else
                {
                    logger.Warning("Nested multipart part missing boundary header.");
                }
            }

            payload.Parts.Add(new KrRawPart
            {
                Name = name,
                ContentType = contentType,
                Length = result.Length,
                TempPath = result.TempPath,
                Headers = headers,
                NestedPayload = nested
            });

            if (string.IsNullOrWhiteSpace(result.TempPath))
            {
                logger.Warning("Ordered part {Index} name={Name} was not stored to disk (bytes={Bytes}).", partIndex - 1, name, result.Length);
            }
            else
            {
                logger.Information("Stored ordered part {Index} name={Name} contentType={ContentType} bytes={Bytes}", partIndex - 1, name, contentType, result.Length);
            }
        }

        logger.Information("Parsed multipart ordered payload with {Parts} parts and {Bytes} bytes.", partIndex, totalBytes);
        return payload;
    }

    private static async Task<KrPartWriteResult> StorePartAsync(Stream body, KrFormOptions options, KrFormPartRule? rule, string? originalFileName, string? contentEncoding, Logger logger, CancellationToken cancellationToken)
    {
        var maxBytes = rule?.MaxBytes ?? options.Limits.MaxPartBodyBytes;
        var effectiveMax = options.EnablePartDecompression ? Math.Min(maxBytes, options.MaxDecompressedBytesPerPart) : maxBytes;

        var source = body;
        if (options.EnablePartDecompression)
        {
            var (decoded, normalizedEncoding) = KrPartDecompression.CreateDecodedStream(body, contentEncoding);
            if (!IsEncodingAllowed(normalizedEncoding, options.AllowedPartContentEncodings))
            {
                var message = $"Unsupported Content-Encoding '{normalizedEncoding}' for multipart part.";
                if (options.RejectUnknownContentEncoding)
                {
                    logger.Error(message);
                    throw new KrFormException(message, StatusCodes.Status415UnsupportedMediaType);
                }
                logger.Warning(message);
            }
            else
            {
                logger.Debug("Part-level decompression enabled for encoding {Encoding}.", normalizedEncoding);
            }
            source = decoded;
        }
        else if (!string.IsNullOrWhiteSpace(contentEncoding) && !contentEncoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
        {
            var message = $"Part Content-Encoding '{contentEncoding}' was supplied but part decompression is disabled.";
            if (options.RejectUnknownContentEncoding)
            {
                logger.Error(message);
                throw new KrFormException(message, StatusCodes.Status415UnsupportedMediaType);
            }
            logger.Warning(message);
        }

        await using var limited = new LimitedReadStream(source, effectiveMax);

        if (rule?.StoreToDisk == false)
        {
            var length = await ConsumeStreamAsync(limited, cancellationToken).ConfigureAwait(false);
            return new KrPartWriteResult
            {
                TempPath = string.Empty,
                Length = length,
                Sha256 = null
            };
        }

        var targetPath = rule?.DestinationPath ?? options.DefaultUploadPath;
        _ = Directory.CreateDirectory(targetPath);
        var sanitizedFileName = string.IsNullOrWhiteSpace(originalFileName) ? null : options.SanitizeFileName(originalFileName);
        var sink = new KrDiskPartSink(targetPath, options.ComputeSha256, sanitizedFileName);
        return await sink.WriteAsync(limited, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadFieldValueAsync(Stream body, KrFormOptions options, string? contentEncoding, Logger logger, CancellationToken cancellationToken)
    {
        var source = body;
        if (options.EnablePartDecompression)
        {
            var (decoded, normalizedEncoding) = KrPartDecompression.CreateDecodedStream(body, contentEncoding);
            if (!IsEncodingAllowed(normalizedEncoding, options.AllowedPartContentEncodings))
            {
                var message = $"Unsupported Content-Encoding '{normalizedEncoding}' for multipart field.";
                if (options.RejectUnknownContentEncoding)
                {
                    logger.Error(message);
                    throw new KrFormException(message, StatusCodes.Status415UnsupportedMediaType);
                }
                logger.Warning(message);
            }
            else
            {
                logger.Debug("Field-level decompression enabled for encoding {Encoding}.", normalizedEncoding);
            }
            source = decoded;
        }

        await using var limited = new LimitedReadStream(source, options.Limits.MaxFieldValueBytes);
        using var reader = new StreamReader(limited, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var value = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return value;
    }

    private static async Task DrainSectionAsync(Stream body, KrFormOptions options, string? contentEncoding, Logger logger, CancellationToken cancellationToken)
    {
        var source = body;
        if (options.EnablePartDecompression)
        {
            var (decoded, normalizedEncoding) = KrPartDecompression.CreateDecodedStream(body, contentEncoding);
            source = decoded;
            logger.Debug("Draining part with encoding {Encoding}.", normalizedEncoding);
        }

        await using var limited = new LimitedReadStream(source, options.Limits.MaxPartBodyBytes);
        await limited.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> ConsumeStreamAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
        }
        return total;
    }

    private static void ValidateFilePart(string? name, string fileName, string contentType, KrFormPartRule? rule, KrFormData payload, Logger logger)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            logger.Error("File part missing name.");
            throw new KrFormException("File part must include a name.", StatusCodes.Status400BadRequest);
        }

        if (rule == null)
        {
            return;
        }

        if (!rule.AllowMultiple && payload.Files.ContainsKey(name))
        {
            logger.Error("Part rule disallows multiple files for name {Name}.", name);
            throw new KrFormException($"Multiple files not allowed for '{name}'.", StatusCodes.Status400BadRequest);
        }

        if (rule.AllowedContentTypes.Count > 0 && !IsAllowedRequestContentType(contentType, rule.AllowedContentTypes))
        {
            logger.Error("Rejected content type {ContentType} for part {Name}.", contentType, name);
            throw new KrFormException("Content type is not allowed for this part.", StatusCodes.Status415UnsupportedMediaType);
        }

        if (rule.AllowedExtensions.Count > 0)
        {
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext) || !rule.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                logger.Error("Rejected extension {Extension} for part {Name}.", ext, name);
                throw new KrFormException("File extension is not allowed for this part.", StatusCodes.Status400BadRequest);
            }
        }

        if (rule.MaxBytes.HasValue && rule.MaxBytes.Value <= 0)
        {
            logger.Warning("Part rule for {Name} has non-positive MaxBytes.", name);
        }
    }

    private static void AppendFile(Dictionary<string, KrFilePart[]> files, KrFilePart part, KrFormPartRule? rule, Logger logger)
    {
        files[part.Name] = files.TryGetValue(part.Name, out var existing)
            ? [.. existing, part]
            : [part];

        if (rule != null && !rule.AllowMultiple && files[part.Name].Length > 1)
        {
            logger.Error("Rule disallows multiple files for {Name}.", part.Name);
            throw new KrFormException($"Multiple files not allowed for '{part.Name}'.", StatusCodes.Status400BadRequest);
        }
    }

    private static void AppendField(Dictionary<string, string[]> fields, string name, string value)
    {
        fields[name] = fields.TryGetValue(name, out var existing)
            ? [.. existing, value]
            : [value];
    }

    private static void ValidateRequiredRules(KrFormData payload, Dictionary<string, KrFormPartRule> rules, Logger logger)
    {
        foreach (var rule in rules.Values)
        {
            if (!rule.Required)
            {
                continue;
            }

            var hasField = payload.Fields.ContainsKey(rule.Name);
            var hasFile = payload.Files.ContainsKey(rule.Name);
            if (!hasField && !hasFile)
            {
                logger.Error("Required form part missing: {Name}", rule.Name);
                throw new KrFormException($"Required form part '{rule.Name}' missing.", StatusCodes.Status400BadRequest);
            }
        }
    }

    private static Dictionary<string, KrFormPartRule> CreateRuleMap(KrFormOptions options)
    {
        var map = new Dictionary<string, KrFormPartRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in options.Rules)
        {
            map[rule.Name] = rule;
        }
        return map;
    }

    private static (string? Name, string? FileName, ContentDispositionHeaderValue? Disposition) GetContentDisposition(MultipartSection section, Logger logger, bool allowMissing = false)
    {
        if (string.IsNullOrWhiteSpace(section.ContentDisposition))
        {
            if (allowMissing)
            {
                return (null, null, null);
            }

            logger.Error("Multipart section missing Content-Disposition header.");
            throw new KrFormException("Missing Content-Disposition header.", StatusCodes.Status400BadRequest);
        }

        if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
        {
            logger.Error("Invalid Content-Disposition header: {Header}", section.ContentDisposition);
            throw new KrFormException("Invalid Content-Disposition header.", StatusCodes.Status400BadRequest);
        }

        var name = disposition.Name.HasValue ? HeaderUtilities.RemoveQuotes(disposition.Name).Value : null;
        var fileName = disposition.FileNameStar.HasValue
            ? HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
            : disposition.FileName.HasValue ? HeaderUtilities.RemoveQuotes(disposition.FileName).Value : null;

        return (name, fileName, disposition);
    }

    private static string GetBoundary(MediaTypeHeaderValue mediaType)
    {
        if (!mediaType.Boundary.HasValue)
        {
            throw new KrFormException("Missing multipart boundary.", StatusCodes.Status400BadRequest);
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        return string.IsNullOrWhiteSpace(boundary)
            ? throw new KrFormException("Missing multipart boundary.", StatusCodes.Status400BadRequest)
            : boundary;
    }

    private static bool TryGetBoundary(string contentType, out string boundary)
    {
        boundary = string.Empty;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return false;
        }

        if (!mediaType.Boundary.HasValue)
        {
            return false;
        }

        var parsed = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        boundary = parsed;
        return true;
    }

    private static Dictionary<string, string[]> ToHeaderDictionary(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> headers)
    {
        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            dict[header.Key] = [.. header.Value.Select(static v => v ?? string.Empty)];
        }
        return dict;
    }

    private static string? GetHeaderValue(IReadOnlyDictionary<string, string[]> headers, string name)
        => headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;

    private static long? GetHeaderLong(IReadOnlyDictionary<string, string[]> headers, string name)
        => headers.TryGetValue(name, out var values) && long.TryParse(values.FirstOrDefault(), out var result)
            ? result
            : null;
    private static bool IsAllowedRequestContentType(string contentType, IEnumerable<string> allowed)
    {
        foreach (var allowedType in allowed)
        {
            if (string.IsNullOrWhiteSpace(allowedType))
            {
                continue;
            }

            if (allowedType.EndsWith("/*", StringComparison.Ordinal))
            {
                var prefix = allowedType[..^1];
                if (contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (contentType.Equals(allowedType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsMultipartContentType(string contentType)
        => contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);

    private static bool IsEncodingAllowed(string encoding, IEnumerable<string> allowed)
        => allowed.Any(a => string.Equals(a, encoding, StringComparison.OrdinalIgnoreCase));

    private static bool DetectRequestDecompressionEnabled(HttpContext context)
    {
        var type = Type.GetType("Microsoft.AspNetCore.RequestDecompression.IRequestDecompressionProvider, Microsoft.AspNetCore.RequestDecompression");
        return type is not null && context.RequestServices.GetService(type) is not null;
    }

    private static async ValueTask<KrPartAction> InvokeOnPartAsync(KrFormOptions options, KrPartContext context, Logger logger)
    {
        if (options.OnPart == null)
        {
            return KrPartAction.Continue;
        }

        try
        {
            return await options.OnPart(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Part hook failed for part {Index}.", context.Index);
            throw new KrFormException("Part hook failed.", StatusCodes.Status400BadRequest);
        }
    }
}

internal static class LoggerExtensions
{
    /// <summary>
    /// Adds a simple timed logging scope.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="operation">The operation name.</param>
    /// <returns>The disposable scope.</returns>
    public static IDisposable BeginTimedOperation(this Logger logger, string operation)
        => new TimedOperation(logger, operation);

    private sealed class TimedOperation : IDisposable
    {
        private readonly Logger _logger;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;

        public TimedOperation(Logger logger, string operation)
        {
            _logger = logger;
            _operation = operation;
            _stopwatch = Stopwatch.StartNew();
            if (_logger.IsEnabled(LogEventLevel.Information))
            {
                _logger.Information("Form parsing started: {Operation}", _operation);
            }
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            if (_logger.IsEnabled(LogEventLevel.Information))
            {
                _logger.Information("Form parsing completed: {Operation} in {ElapsedMs} ms", _operation, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
