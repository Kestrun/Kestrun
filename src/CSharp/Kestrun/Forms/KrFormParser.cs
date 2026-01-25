using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Kestrun.Forms;

/// <summary>
/// Main entry point for parsing form requests.
/// </summary>
public static class KrFormParser
{
    /// <summary>
    /// Default buffer size for streaming operations (80 KB).
    /// </summary>
    private const int DefaultBufferSize = 81920;

    /// <summary>
    /// Checks if the encoding requires decompression (not identity).
    /// </summary>
    private static bool IsCompressionEncoding(string? encoding)
    {
        return !string.IsNullOrWhiteSpace(encoding) 
               && !encoding.Equals("identity", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes decompression for a part if needed based on Content-Encoding header.
    /// </summary>
    private static (Stream Stream, bool IsDecompressing) InitializePartDecompression(
        Stream bodyStream,
        Dictionary<string, StringValues>? headers,
        KrFormOptions options,
        Serilog.ILogger logger,
        int partIndex)
    {
        var isDecompressing = false;

        if (options.EnablePartDecompression && 
            headers != null && 
            headers.TryGetValue("Content-Encoding", out var partContentEncoding))
        {
            var encoding = partContentEncoding.ToString();
            logger.Debug("Part {PartIndex} has Content-Encoding: {Encoding}", partIndex, encoding);

            if (IsCompressionEncoding(encoding))
            {
                if (!options.AllowedPartContentEncodings.Contains(encoding, StringComparer.OrdinalIgnoreCase))
                {
                    if (options.RejectUnknownContentEncoding)
                    {
                        logger.Error("Unknown part Content-Encoding: {Encoding}", encoding);
                        throw new BadHttpRequestException($"Unsupported part Content-Encoding: {encoding}", 
                            StatusCodes.Status415UnsupportedMediaType);
                    }
                    logger.Warning("Unknown part Content-Encoding allowed: {Encoding}", encoding);
                }
                else
                {
                    try
                    {
                        (bodyStream, isDecompressing) = KrPartDecompression.WrapWithDecompression(
                            bodyStream,
                            encoding,
                            options.MaxDecompressedBytesPerPart,
                            logger);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to initialize part decompression for part {PartIndex}", partIndex);
                        throw new BadHttpRequestException("Failed to decompress part.", StatusCodes.Status400BadRequest);
                    }
                }
            }
        }

        return (bodyStream, isDecompressing);
    }
    /// <summary>
    /// Parses a form request according to its content type.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="options">The form parsing options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A KrFormContext containing the parsed payload.</returns>
    public static async Task<KrFormContext> ParseAsync(
        HttpContext httpContext,
        KrFormOptions options,
        Serilog.ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var stopwatch = Stopwatch.StartNew();
        var request = httpContext.Request;
        var contentTypeHeader = request.ContentType;

        logger.Information("Form parsing started: ContentType={ContentType}, ContentLength={ContentLength}, Path={Path}",
            contentTypeHeader ?? "(null)", request.ContentLength, request.Path);

        // Log Content-Encoding if present
        if (request.Headers.TryGetValue("Content-Encoding", out var contentEncoding))
        {
            logger.Information("Request Content-Encoding detected: {ContentEncoding}", contentEncoding.ToString());
        }

        // Parse content type
        if (!MediaTypeHeaderValue.TryParse(contentTypeHeader, out var mediaType) || mediaType.MediaType == null)
        {
            logger.Error("Invalid or missing Content-Type header");
            throw new BadHttpRequestException("Invalid or missing Content-Type header.", StatusCodes.Status400BadRequest);
        }

        // Validate content type
        var mediaTypeValue = mediaType.MediaType ?? string.Empty;
        if (!IsContentTypeAllowed(mediaTypeValue, options.AllowedRequestContentTypes))
        {
            if (options.RejectUnknownRequestContentType)
            {
                logger.Error("Unsupported Content-Type: {ContentType}", mediaTypeValue);
                throw new BadHttpRequestException($"Unsupported Content-Type: {mediaTypeValue}", StatusCodes.Status415UnsupportedMediaType);
            }
            logger.Warning("Unknown Content-Type allowed: {ContentType}", mediaTypeValue);
        }

        // Set max request body size if possible
        if (options.MaxRequestBodyBytes.HasValue)
        {
            var maxBodyFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxBodyFeature != null && !maxBodyFeature.IsReadOnly)
            {
                maxBodyFeature.MaxRequestBodySize = options.MaxRequestBodyBytes.Value;
                logger.Debug("Max request body size set to {MaxBytes} bytes", options.MaxRequestBodyBytes.Value);
            }
        }

        KrFormPayload payload;

        // Route to appropriate parser based on content type
        if (mediaTypeValue.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            payload = await ParseUrlEncodedAsync(httpContext, options, logger, cancellationToken);
        }
        else if (mediaTypeValue.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = GetBoundary(mediaType);
            payload = await ParseMultipartFormDataAsync(httpContext, options, boundary, logger, nestingDepth: 0, cancellationToken);
        }
        else if (mediaTypeValue.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = GetBoundary(mediaType);
            payload = await ParseMultipartOrderedAsync(httpContext, options, boundary, logger, nestingDepth: 0, cancellationToken);
        }
        else
        {
            logger.Error("Unhandled Content-Type: {ContentType}", mediaTypeValue);
            throw new BadHttpRequestException($"Unhandled Content-Type: {mediaTypeValue}", StatusCodes.Status400BadRequest);
        }

        stopwatch.Stop();
        logger.Information("Form parsing completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        return new KrFormContext
        {
            HttpContext = httpContext,
            Options = options,
            Payload = payload,
            Logger = logger
        };
    }

    /// <summary>
    /// Parses application/x-www-form-urlencoded request.
    /// </summary>
    private static async Task<KrNamedPartsPayload> ParseUrlEncodedAsync(
        HttpContext httpContext,
        KrFormOptions options,
        Serilog.ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.Debug("Parsing application/x-www-form-urlencoded");

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var fields = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in form)
        {
            var values = kvp.Value.ToArray();
            
            // Enforce field value size limit
            foreach (var value in values)
            {
                if (value != null && value.Length > options.MaxFieldValueBytes)
                {
                    logger.Error("Field {FieldName} exceeds MaxFieldValueBytes: {ActualBytes} > {MaxBytes}",
                        kvp.Key, value.Length, options.MaxFieldValueBytes);
                    throw new BadHttpRequestException($"Field '{kvp.Key}' exceeds maximum size.", StatusCodes.Status413RequestEntityTooLarge);
                }
            }

            fields[kvp.Key] = values!;
            logger.Debug("Field parsed: {FieldName}, Values: {ValueCount}", kvp.Key, values.Length);
        }

        logger.Information("URL-encoded form parsed: {FieldCount} fields", fields.Count);

        return new KrNamedPartsPayload
        {
            Fields = fields,
            Files = new Dictionary<string, KrFilePart[]>()
        };
    }

    /// <summary>
    /// Parses multipart/form-data request.
    /// </summary>
    private static async Task<KrNamedPartsPayload> ParseMultipartFormDataAsync(
        HttpContext httpContext,
        KrFormOptions options,
        string boundary,
        Serilog.ILogger logger,
        int nestingDepth,
        CancellationToken cancellationToken)
    {
        logger.Debug("Parsing multipart/form-data with boundary: {Boundary}, NestingDepth: {Depth}", boundary, nestingDepth);

        var reader = new MultipartReader(boundary, httpContext.Request.Body);
        var fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var files = new Dictionary<string, List<KrFilePart>>(StringComparer.OrdinalIgnoreCase);
        var partIndex = 0;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) != null)
        {
            if (partIndex >= options.MaxParts)
            {
                logger.Error("Max parts limit exceeded: {MaxParts}", options.MaxParts);
                throw new BadHttpRequestException($"Maximum number of parts ({options.MaxParts}) exceeded.", StatusCodes.Status413RequestEntityTooLarge);
            }

            var contentDisposition = section.GetContentDispositionHeader();
            if (contentDisposition == null)
            {
                logger.Warning("Part {PartIndex} missing Content-Disposition header, skipping", partIndex);
                partIndex++;
                continue;
            }

            var name = contentDisposition.Name.Value;
            var filename = contentDisposition.FileName.Value;
            var contentType = section.ContentType;

            logger.Debug("Part {PartIndex}: Name={Name}, FileName={FileName}, ContentType={ContentType}",
                partIndex, name, filename ?? "(none)", contentType ?? "(none)");

            // Create part context for hooks
            var partContext = new KrPartContext
            {
                FormContext = null!, // Will be set after full parse
                PartIndex = partIndex,
                PartName = name,
                OriginalFileName = filename,
                ContentType = contentType,
                Headers = section.Headers
            };

            // Call OnPart hook if provided
            var action = KrPartAction.Continue;
            if (options.OnPart != null)
            {
                action = await options.OnPart(partContext);
                logger.Debug("OnPart hook returned: {Action} for part {PartIndex}", action, partIndex);
            }

            if (action == KrPartAction.Reject)
            {
                logger.Error("Part {PartIndex} rejected by OnPart hook", partIndex);
                throw new BadHttpRequestException("Part rejected by validation.", StatusCodes.Status400BadRequest);
            }

            if (action == KrPartAction.Skip)
            {
                logger.Information("Part {PartIndex} skipped by OnPart hook", partIndex);
                partIndex++;
                continue;
            }

            // Find matching rule
            var rule = options.Rules.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            // Determine if this is a file or text field
            if (string.IsNullOrWhiteSpace(filename))
            {
                // Text field
                var maxFieldBytes = rule?.MaxBytes ?? options.MaxFieldValueBytes;
                var fieldValue = await ReadTextFieldAsync(section.Body, maxFieldBytes, logger, partIndex, cancellationToken);
                
                if (!fields.ContainsKey(name!))
                {
                    fields[name!] = new List<string>();
                }
                fields[name!].Add(fieldValue);
                logger.Debug("Text field stored: {FieldName}, Length: {Length}", name, fieldValue.Length);
            }
            else
            {
                // File field
                var filePart = await ProcessFilePartAsync(section, name!, filename, contentType, rule, options, logger, partIndex, nestingDepth, cancellationToken);
                
                if (!files.ContainsKey(name!))
                {
                    files[name!] = new List<KrFilePart>();
                }

                // Check AllowMultiple
                if (rule != null && !rule.AllowMultiple && files[name!].Count > 0)
                {
                    logger.Error("Part {PartName} does not allow multiple files", name);
                    throw new BadHttpRequestException($"Part '{name}' does not allow multiple files.", StatusCodes.Status400BadRequest);
                }

                files[name!].Add(filePart);
                logger.Information("File part stored: {FieldName}, FileName: {FileName}, Size: {Size} bytes, Sha256: {Sha256}",
                    name, filePart.OriginalFileName, filePart.Length, filePart.Sha256 ?? "(not computed)");
            }

            partIndex++;
        }

        // Validate required parts
        foreach (var rule in options.Rules.Where(r => r.Required))
        {
            if (!fields.ContainsKey(rule.Name) && !files.ContainsKey(rule.Name))
            {
                logger.Error("Required part missing: {PartName}", rule.Name);
                throw new BadHttpRequestException($"Required part '{rule.Name}' is missing.", StatusCodes.Status400BadRequest);
            }
        }

        logger.Information("Multipart form-data parsed: {FieldCount} fields, {FileCount} file fields, {TotalParts} parts",
            fields.Count, files.Count, partIndex);

        return new KrNamedPartsPayload
        {
            Fields = fields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
            Files = files.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
        };
    }

    /// <summary>
    /// Parses multipart/mixed and other ordered multipart types.
    /// </summary>
    private static async Task<KrOrderedPartsPayload> ParseMultipartOrderedAsync(
        HttpContext httpContext,
        KrFormOptions options,
        string boundary,
        Serilog.ILogger logger,
        int nestingDepth,
        CancellationToken cancellationToken)
    {
        logger.Debug("Parsing ordered multipart with boundary: {Boundary}, NestingDepth: {Depth}", boundary, nestingDepth);

        var reader = new MultipartReader(boundary, httpContext.Request.Body);
        var parts = new List<KrRawPart>();
        var partIndex = 0;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) != null)
        {
            if (partIndex >= options.MaxParts)
            {
                logger.Error("Max parts limit exceeded: {MaxParts}", options.MaxParts);
                throw new BadHttpRequestException($"Maximum number of parts ({options.MaxParts}) exceeded.", StatusCodes.Status413RequestEntityTooLarge);
            }

            var contentType = section.ContentType;
            var contentDisposition = section.GetContentDispositionHeader();
            var name = contentDisposition?.Name.Value;

            logger.Debug("Ordered part {PartIndex}: Name={Name}, ContentType={ContentType}",
                partIndex, name ?? "(none)", contentType ?? "(none)");

            // Check for nested multipart
            KrFormPayload? nestedPayload = null;
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) &&
                nestingDepth < options.MaxNestingDepth)
            {
                logger.Information("Nested multipart detected in part {PartIndex}, ContentType: {ContentType}", partIndex, contentType);
                
                // Parse nested multipart
                if (MediaTypeHeaderValue.TryParse(contentType, out var nestedMediaType))
                {
                    var nestedBoundary = GetBoundary(nestedMediaType);
                    nestedPayload = await ParseMultipartFromStreamAsync(section.Body, nestedBoundary, options, logger, nestingDepth + 1, cancellationToken);
                    logger.Information("Nested multipart parsed successfully in part {PartIndex}", partIndex);
                }
            }

            KrRawPart rawPart;

            if (nestedPayload != null)
            {
                // For nested multipart, we don't store the body to disk (it's already parsed)
                rawPart = new KrRawPart
                {
                    Name = name,
                    ContentType = contentType,
                    Length = 0, // Not applicable for nested
                    TempPath = string.Empty, // Not stored to disk
                    Headers = section.Headers,
                    NestedPayload = nestedPayload
                };
            }
            else
            {
                // Store non-nested part to disk
                var partResult = await ProcessRawPartAsync(section, name, contentType, options, logger, partIndex, nestingDepth, cancellationToken);
                rawPart = partResult;
            }

            parts.Add(rawPart);
            partIndex++;
        }

        logger.Information("Ordered multipart parsed: {PartCount} parts", parts.Count);

        return new KrOrderedPartsPayload
        {
            Parts = parts
        };
    }

    /// <summary>
    /// Parses multipart from a stream (used for nested multipart).
    /// </summary>
    private static async Task<KrFormPayload> ParseMultipartFromStreamAsync(
        Stream bodyStream,
        string boundary,
        KrFormOptions options,
        Serilog.ILogger logger,
        int nestingDepth,
        CancellationToken cancellationToken)
    {
        logger.Debug("Parsing nested multipart from stream, Boundary: {Boundary}, NestingDepth: {Depth}", boundary, nestingDepth);

        if (nestingDepth > options.MaxNestingDepth)
        {
            logger.Error("Max nesting depth exceeded: {MaxDepth}", options.MaxNestingDepth);
            throw new BadHttpRequestException($"Maximum nesting depth ({options.MaxNestingDepth}) exceeded.", StatusCodes.Status400BadRequest);
        }

        var reader = new MultipartReader(boundary, bodyStream);
        var parts = new List<KrRawPart>();
        var partIndex = 0;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) != null)
        {
            if (partIndex >= options.MaxParts)
            {
                logger.Error("Max parts limit exceeded in nested multipart: {MaxParts}", options.MaxParts);
                throw new BadHttpRequestException($"Maximum number of parts ({options.MaxParts}) exceeded.", StatusCodes.Status413RequestEntityTooLarge);
            }

            var contentType = section.ContentType;
            var contentDisposition = section.GetContentDispositionHeader();
            var name = contentDisposition?.Name.Value;

            logger.Debug("Nested part {PartIndex}: Name={Name}, ContentType={ContentType}", partIndex, name ?? "(none)", contentType ?? "(none)");

            var rawPart = await ProcessRawPartAsync(section, name, contentType, options, logger, partIndex, nestingDepth, cancellationToken);
            parts.Add(rawPart);
            partIndex++;
        }

        logger.Debug("Nested multipart completed: {PartCount} parts", parts.Count);

        return new KrOrderedPartsPayload
        {
            Parts = parts
        };
    }

    /// <summary>
    /// Processes a file part and stores it to disk.
    /// </summary>
    private static async Task<KrFilePart> ProcessFilePartAsync(
        MultipartSection section,
        string name,
        string filename,
        string? contentType,
        KrPartRule? rule,
        KrFormOptions options,
        Serilog.ILogger logger,
        int partIndex,
        int nestingDepth,
        CancellationToken cancellationToken)
    {
        // Sanitize filename
        var sanitizedFilename = options.SanitizeFileName(filename);
        
        // Validate extension
        if (rule?.AllowedExtensions != null && rule.AllowedExtensions.Count > 0)
        {
            var extension = Path.GetExtension(sanitizedFilename);
            if (!rule.AllowedExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                logger.Error("File extension not allowed for part {PartName}: {Extension}, Allowed: {AllowedExtensions}",
                    name, extension, string.Join(", ", rule.AllowedExtensions));
                throw new BadHttpRequestException($"File extension '{extension}' not allowed for part '{name}'.", StatusCodes.Status400BadRequest);
            }
        }

        // Validate content type
        if (rule?.AllowedContentTypes != null && rule.AllowedContentTypes.Count > 0 && !string.IsNullOrWhiteSpace(contentType))
        {
            if (!IsContentTypeAllowed(contentType, rule.AllowedContentTypes))
            {
                logger.Error("Content type not allowed for part {PartName}: {ContentType}, Allowed: {AllowedContentTypes}",
                    name, contentType, string.Join(", ", rule.AllowedContentTypes));
                throw new BadHttpRequestException($"Content type '{contentType}' not allowed for part '{name}'.", StatusCodes.Status400BadRequest);
            }
        }

        // Determine max bytes
        var maxBytes = rule?.MaxBytes ?? options.MaxPartBodyBytes;

        // Determine destination path
        var destinationPath = rule?.DestinationPath ?? options.DefaultUploadPath;

        // Handle part-level decompression
        var (decompressedStream, isDecompressing) = InitializePartDecompression(
            section.Body,
            section.Headers,
            options,
            logger,
            partIndex);

        // Stream to disk
        await using var sink = new DiskSink(destinationPath, options.ComputeSha256);

        try
        {
            var buffer = new byte[DefaultBufferSize];
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await decompressedStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalBytesRead += bytesRead;

                // Check limit after decompression (if decompressing, LimitedReadStream already enforces it)
                if (!isDecompressing && totalBytesRead > maxBytes)
                {
                    logger.Error("Part {PartIndex} exceeds MaxPartBodyBytes: {ActualBytes} > {MaxBytes}", partIndex, totalBytesRead, maxBytes);
                    throw new BadHttpRequestException($"Part '{name}' exceeds maximum size.", StatusCodes.Status413RequestEntityTooLarge);
                }

                await sink.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            var (tempPath, length, sha256) = await sink.CompleteAsync(cancellationToken);

            logger.Debug("File part {PartIndex} written to disk: {TempPath}, Size: {Size} bytes, Decompressed: {Decompressed}",
                partIndex, tempPath, length, isDecompressing);

            return new KrFilePart
            {
                Name = name,
                OriginalFileName = sanitizedFilename,
                ContentType = contentType,
                Length = length,
                TempPath = tempPath,
                Sha256 = sha256,
                Headers = section.Headers
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Decompressed size limit"))
        {
            logger.Error("Decompressed size limit exceeded for part {PartIndex}: {Message}", partIndex, ex.Message);
            throw new BadHttpRequestException("Decompressed size limit exceeded.", StatusCodes.Status413RequestEntityTooLarge);
        }
    }

    /// <summary>
    /// Processes a raw part for ordered multipart.
    /// </summary>
    private static async Task<KrRawPart> ProcessRawPartAsync(
        MultipartSection section,
        string? name,
        string? contentType,
        KrFormOptions options,
        Serilog.ILogger logger,
        int partIndex,
        int nestingDepth,
        CancellationToken cancellationToken)
    {
        var destinationPath = options.DefaultUploadPath;

        // Handle part-level decompression
        var (decompressedStream, isDecompressing) = InitializePartDecompression(
            section.Body,
            section.Headers,
            options,
            logger,
            partIndex);

        // Stream to disk
        await using var sink = new DiskSink(destinationPath, options.ComputeSha256);

        try
        {
            var buffer = new byte[DefaultBufferSize];
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await decompressedStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalBytesRead += bytesRead;

                if (!isDecompressing && totalBytesRead > options.MaxPartBodyBytes)
                {
                    logger.Error("Raw part {PartIndex} exceeds MaxPartBodyBytes: {ActualBytes} > {MaxBytes}",
                        partIndex, totalBytesRead, options.MaxPartBodyBytes);
                    throw new BadHttpRequestException("Part exceeds maximum size.", StatusCodes.Status413RequestEntityTooLarge);
                }

                await sink.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            var (tempPath, length, sha256) = await sink.CompleteAsync(cancellationToken);

            logger.Debug("Raw part {PartIndex} written to disk: {TempPath}, Size: {Size} bytes, Decompressed: {Decompressed}",
                partIndex, tempPath, length, isDecompressing);

            return new KrRawPart
            {
                Name = name,
                ContentType = contentType,
                Length = length,
                TempPath = tempPath,
                Headers = section.Headers
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Decompressed size limit"))
        {
            logger.Error("Decompressed size limit exceeded for raw part {PartIndex}: {Message}", partIndex, ex.Message);
            throw new BadHttpRequestException("Decompressed size limit exceeded.", StatusCodes.Status413RequestEntityTooLarge);
        }
    }

    /// <summary>
    /// Reads a text field from a section body.
    /// </summary>
    private static async Task<string> ReadTextFieldAsync(
        Stream bodyStream,
        long maxBytes,
        Serilog.ILogger logger,
        int partIndex,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        long totalBytesRead = 0;

        int bytesRead;
        while ((bytesRead = await bodyStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalBytesRead += bytesRead;
            if (totalBytesRead > maxBytes)
            {
                logger.Error("Text field at part {PartIndex} exceeds max field bytes: {ActualBytes} > {MaxBytes}",
                    partIndex, totalBytesRead, maxBytes);
                throw new BadHttpRequestException("Text field exceeds maximum size.", StatusCodes.Status413RequestEntityTooLarge);
            }

            ms.Write(buffer, 0, bytesRead);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Checks if a content type matches the allowed list (supports wildcards).
    /// </summary>
    private static bool IsContentTypeAllowed(string contentType, List<string> allowedContentTypes)
    {
        foreach (var allowed in allowedContentTypes)
        {
            if (allowed.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Support wildcard matching (e.g., image/*)
            if (allowed.EndsWith("/*"))
            {
                var prefix = allowed[..^2];
                if (contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the boundary parameter from a multipart content type.
    /// </summary>
    private static string GetBoundary(MediaTypeHeaderValue mediaType)
    {
        var boundary = mediaType.Parameters
            .FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim('"');

        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new BadHttpRequestException("Missing boundary parameter in Content-Type.", StatusCodes.Status400BadRequest);
        }

        return boundary;
    }
}
