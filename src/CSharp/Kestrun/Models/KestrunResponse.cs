
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kestrun.Utilities.Json;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using Serilog.Events;
using System.Buffers;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using MongoDB.Bson;
using Kestrun.Utilities;
using System.Collections;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper;
using System.Reflection;
using Microsoft.Net.Http.Headers;
using Kestrun.Utilities.Yaml;
using Kestrun.Hosting.Options;
using Kestrun.Callback;
using System.Management.Automation;
using Kestrun.Logging;

namespace Kestrun.Models;

/// <summary>
/// Represents an HTTP response in the Kestrun framework, providing methods to write various content types and manage headers, cookies, and status codes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunResponse"/> class with the specified request and optional body async threshold.
/// </remarks>
public class KestrunResponse
{
    private static readonly ContentTypeWithSchema[] LegacyNegotiatedResponseContentTypes =
    [
        new("*/*")
    ];

    /// <summary>
    /// Flag indicating whether callbacks have already been enqueued.
    /// </summary>
    internal int CallbacksEnqueuedFlag; // 0 = no, 1 = yes

    /// <summary>
    ///     Gets the list of callback requests associated with this response.
    /// </summary>
    public List<CallBackExecutionPlan> CallbackPlan { get; } = [];

    private Serilog.ILogger Logger => KrContext.Host.Logger;
    /// <summary>
    /// Gets the route options associated with this response.
    /// </summary>
    public MapRouteOptions MapRouteOptions => KrContext.MapRouteOptions;
    /// <summary>
    /// Gets the associated KestrunContext for this response.
    /// </summary>
    public required KestrunContext KrContext { get; init; }

    /// <summary>
    /// Gets the KestrunHost associated with this response.
    /// </summary>
    public Hosting.KestrunHost Host => KrContext.Host;
    /// <summary>
    /// A set of MIME types that are considered text-based for response content.
    /// </summary>
    public static readonly HashSet<string> TextBasedMimeTypes =
#pragma warning disable IDE0028 // Simplify collection initialization
    new(StringComparer.OrdinalIgnoreCase)
#pragma warning restore IDE0028 // Simplify collection initialization
    {
        "application/json",
        "application/xml",
        "application/javascript",
        "application/xhtml+xml",
        "application/x-www-form-urlencoded",
        "application/yaml",
        "application/graphql"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunResponse"/> class.
    /// </summary>
    /// <param name="krContext">The associated <see cref="KestrunContext"/> for this response.</param>
    /// <param name="bodyAsyncThreshold">The threshold in bytes for using async body write operations. Defaults to 8192.</param>
    [SetsRequiredMembers]
    public KestrunResponse(KestrunContext krContext, int bodyAsyncThreshold = 8192)
    {
        KrContext = krContext;
        BodyAsyncThreshold = bodyAsyncThreshold;
        Request = KrContext.Request ?? throw new ArgumentNullException(nameof(KrContext));
        AcceptCharset = KrContext.Request.Headers.TryGetValue("Accept-Charset", out var value) ? Encoding.GetEncoding(value) : Encoding.UTF8; // Default to UTF-8 if null
        StatusCode = KrContext.HttpContext.Response.StatusCode;
    }

    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the response.
    /// </summary>
    public HttpContext Context => KrContext.HttpContext;
    /// <summary>
    /// Gets or sets the HTTP status code for the response.
    /// </summary>
    public int StatusCode { get; set; }
    /// <summary>
    /// Gets or sets the collection of HTTP headers for the response.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];
    /// <summary>
    /// Gets or sets the MIME content type of the response.
    /// </summary>
    public string? ContentType { get; set; } = "text/plain";
    /// <summary>
    /// Gets or sets the body of the response, which can be a string, byte array, stream, or file info.
    /// </summary>
    public object? Body { get; set; }
    /// <summary>
    /// Gets or sets the URL to redirect the response to, if an HTTP redirect is required.
    /// </summary>
    public string? RedirectUrl { get; set; } // For HTTP redirects
    /// <summary>
    /// Gets or sets the list of Set-Cookie header values for the response.
    /// </summary>
    public List<string>? Cookies { get; set; } // For Set-Cookie headers

    /// <summary>
    /// Text encoding for textual MIME types.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Content-Disposition header value.
    /// </summary>
    public ContentDispositionOptions ContentDisposition { get; set; } = new ContentDispositionOptions();
    /// <summary>
    /// Gets the associated KestrunRequest for this response.
    /// </summary>
    public required KestrunRequest Request { get; init; }

    /// <summary>
    /// Global text encoding for all responses. Defaults to UTF-8.
    /// </summary>
    public required Encoding AcceptCharset { get; init; }

    /// <summary>
    /// If the response body is larger than this threshold (in bytes), async write will be used.
    /// </summary>
    public required int BodyAsyncThreshold { get; init; }

    /// <summary>
    /// Cache-Control header value for the response.
    /// </summary>
    public CacheControlHeaderValue? CacheControl { get; set; }

    /// <summary>
    /// Represents a simple object for writing responses with a value and status code.
    /// </summary>
    /// <param name="Value">The value to be written in the response.</param>
    /// <param name="Status">The HTTP status code for the response.</param>
    /// <param name="Error">An optional error code to include in the response.</param>
    public record WriteObject(object? Value, int Status = StatusCodes.Status200OK, int? Error = null);

    /// <summary>
    /// Gets or sets a postponed write object that can be used for deferred response writing, allowing the response to be constructed in multiple stages or after certain operations are completed.
    /// </summary>
    public WriteObject PostPonedWriteObject { get; set; } = new WriteObject(null, StatusCodes.Status200OK);

    /// <summary>
    /// Indicates whether there is a postponed write object with a non-null value, which can be used to determine if a deferred response write is pending.
    /// </summary>
    public bool HasPostPonedWriteObject => PostPonedWriteObject.Value is not null;
    #region Constructors
    #endregion

    #region Helpers
    private static string GetSafeCurrentDirectoryOrBaseDirectory()
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or DirectoryNotFoundException
                                   or FileNotFoundException)
        {
            // On Unix/macOS, getcwd() can throw if the process CWD was deleted.
            // We use AppContext.BaseDirectory as a stable fallback to avoid crashing in diagnostics
            // and when resolving relative paths.
            return AppContext.BaseDirectory;
        }
    }

    private static string GetSafeCurrentDirectoryForLogging() => GetSafeCurrentDirectoryOrBaseDirectory();

    /// <summary>
    /// Retrieves the value of the specified header from the response headers.
    /// </summary>
    /// <param name="key">The name of the header to retrieve.</param>
    /// <returns>The value of the header if found; otherwise, null.</returns>
    public string? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Determines whether the specified content type is text-based or supports a charset.
    /// </summary>
    /// <param name="type">The MIME content type to check.</param>
    /// <returns>True if the content type is text-based; otherwise, false.</returns>
    public bool IsTextBasedContentType(string type)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Checking if content type is text-based: {ContentType}", type);
        }

        // Check if the content type is text-based or has a charset
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (type == "application/x-www-form-urlencoded")
        {
            return true;
        }

        // Include structured types using XML or JSON suffixes
        if (type.EndsWith("xml", StringComparison.OrdinalIgnoreCase) ||
            type.EndsWith("json", StringComparison.OrdinalIgnoreCase) ||
            type.EndsWith("yaml", StringComparison.OrdinalIgnoreCase) ||
            type.EndsWith("csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Common application types where charset makes sense
        return TextBasedMimeTypes.Contains(type);
    }
    /// <summary>
    /// Adds callback parameters for the specified callback ID, body, and parameters.
    /// </summary>
    /// <param name="callbackId">The identifier for the callback</param>
    /// <param name="bodyParameterName">The name of the body parameter, if any</param>
    /// <param name="parameters">The parameters for the callback</param>
    public void AddCallbackParameters(string callbackId, string? bodyParameterName, Dictionary<string, object?> parameters)
    {
        if (MapRouteOptions.CallbackPlan is null || MapRouteOptions.CallbackPlan.Count == 0)
        {
            return;
        }
        var plan = MapRouteOptions.CallbackPlan.FirstOrDefault(p => p.CallbackId == callbackId);
        if (plan is null)
        {
            Logger.Warning("CallbackPlan '{id}' not found.", callbackId);
            return;
        }
        // Create a new execution plan
        var newExecutionPlan = new CallBackExecutionPlan(
            CallbackId: callbackId,
            Plan: plan,
            BodyParameterName: bodyParameterName,
            Parameters: parameters
        );

        CallbackPlan.Add(newExecutionPlan);
    }
    #endregion

    #region  Response Writers
    /// <summary>
    /// Writes a file response with the specified file path, content type, and HTTP status code.
    /// </summary>
    /// <param name="filePath">The path to the file to be sent in the response.</param>
    /// <param name="contentType">The MIME type of the file content.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteFileResponse(
        string? filePath,
        string? contentType,
        int statusCode = StatusCodes.Status200OK
    )
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing file response,FilePath={FilePath} StatusCode={StatusCode}, ContentType={ContentType}, CurrentDirectory={CurrentDirectory}",
                filePath, statusCode, contentType, GetSafeCurrentDirectoryForLogging());
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        // IMPORTANT:
        // - Path.GetFullPath(relative) uses the process CWD.
        // - If the CWD is missing/deleted (can occur in CI/test scenarios), GetFullPath can fail.
        // Resolve relative paths against a safe, existing base directory instead.
        var fullPath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(filePath, GetSafeCurrentDirectoryOrBaseDirectory());

        if (!File.Exists(fullPath))
        {
            StatusCode = StatusCodes.Status404NotFound;
            Body = $"File not found: {filePath}";
            ContentType = $"text/plain; charset={Encoding.WebName}";
            return;
        }

        // 2. Extract the directory to use as the "root"
        var directory = Path.GetDirectoryName(fullPath)
                       ?? throw new InvalidOperationException("Could not determine directory from file path");

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Serving file: {FilePath}", fullPath);
        }

        // Create a physical file provider for the directory
        var physicalProvider = new PhysicalFileProvider(directory);
        var fi = physicalProvider.GetFileInfo(Path.GetFileName(fullPath));
        var provider = new FileExtensionContentTypeProvider();
        contentType ??= provider.TryGetContentType(fullPath, out var ct)
                ? ct
                : "application/octet-stream";
        Body = fi;

        // headers & metadata
        StatusCode = statusCode;
        ContentType = contentType;
        Logger.Debug("File response prepared: FileName={FileName}, Length={Length}, ContentType={ContentType}",
            fi.Name, fi.Length, ContentType);
    }

    /// <summary>
    /// Writes a JSON response with the specified input object and HTTP status code.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteJsonResponse(object? inputObject, int statusCode = StatusCodes.Status200OK) => WriteJsonResponseAsync(inputObject, depth: 10, compress: false, statusCode: statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a JSON response with the specified input object and HTTP status code.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteJsonResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) => await WriteJsonResponseAsync(inputObject, depth: 10, compress: false, statusCode: statusCode, contentType: contentType);

    /// <summary>
    /// Writes a JSON response using the specified input object and serializer settings.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="serializerOptions">The options to use for JSON serialization.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteJsonResponse(object? inputObject, JsonSerializerOptions serializerOptions, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteJsonResponseAsync(inputObject, serializerOptions, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a JSON response using the specified input object and serializer settings.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="serializerOptions">The options to use for JSON serialization.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteJsonResponseAsync(object? inputObject, JsonSerializerOptions serializerOptions, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing JSON response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        ArgumentNullException.ThrowIfNull(serializerOptions);

        var sanitizedPayload = PayloadSanitizer.Sanitize(inputObject);
        Body = await Task.Run(() => JsonSerializer.Serialize(sanitizedPayload, serializerOptions));
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/json; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }
    /// <summary>
    /// Writes a JSON response with the specified input object, serialization depth, compression option, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="depth">The maximum depth for JSON serialization.</param>
    /// <param name="compress">Whether to compress the JSON output (no indentation).</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteJsonResponse(object? inputObject, int depth, bool compress, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteJsonResponseAsync(inputObject, depth, compress, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a JSON response with the specified input object, serialization depth, compression option, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="depth">The maximum depth for JSON serialization.</param>
    /// <param name="compress">Whether to compress the JSON output (no indentation).</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteJsonResponseAsync(object? inputObject, int depth, bool compress, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing JSON response (async), StatusCode={StatusCode}, ContentType={ContentType}, Depth={Depth}, Compress={Compress}",
                statusCode, contentType, depth, compress);
        }

        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = !compress,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = depth
        };

        await WriteJsonResponseAsync(inputObject, serializerOptions: serializerOptions, statusCode: statusCode, contentType: contentType);
    }
    /// <summary>
    /// Writes a CBOR response (binary, efficient, not human-readable).
    /// </summary>
    public async Task WriteCborResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing CBOR response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        // Serialize to CBOR using PeterO.Cbor
        Body = await Task.Run(() => inputObject != null
            ? PeterO.Cbor.CBORObject.FromObject(inputObject).EncodeToBytes()
            : []);
        ContentType = string.IsNullOrEmpty(contentType) ? "application/cbor" : contentType;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Writes a CBOR response (binary, efficient, not human-readable).
    /// </summary>
    /// <param name="inputObject">The object to be converted to CBOR.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteCborResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteCborResponseAsync(inputObject, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a BSON response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to BSON.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteBsonResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing BSON response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        // Serialize to BSON (as byte[])
        Body = await Task.Run(() => inputObject != null ? inputObject.ToBson() : []);
        ContentType = string.IsNullOrEmpty(contentType) ? "application/bson" : contentType;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Writes a BSON response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to BSON.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteBsonResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteBsonResponseAsync(inputObject, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Writes a response with the specified input object and HTTP status code.
    /// Chooses the response format based on the Accept header or defaults to text/plain.
    /// </summary>
    /// <param name="inputObject">The object to be sent in the response body.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteResponse(object? inputObject, int statusCode = StatusCodes.Status200OK) => WriteResponseAsync(inputObject, statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a response with the specified input object and HTTP status code.
    /// </summary>
    /// <param name="inputObject">The object to be sent in the response body.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteResponseAsync(WriteObject inputObject) => await WriteResponseAsync(inputObject.Value, inputObject.Status);

    /// <summary>
    /// Asynchronously writes a response with the specified input object and HTTP status code.
    /// Chooses the response format based on the Accept header or defaults to text/plain.
    /// </summary>
    /// <param name="inputObject">The object to be sent in the response body.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        if (inputObject is null)
        {
            throw new ArgumentNullException(nameof(inputObject), "Input object cannot be null. Use WriteResponseAsync(WriteObject) to specify a null value with a status code.");
        }

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing response, StatusCode={StatusCode}", statusCode);
        }

        Body = inputObject;

        try
        {
            // Read Accept header (may be missing)
            string? acceptHeader = null;
            _ = Request?.Headers.TryGetValue(HeaderNames.Accept, out acceptHeader);

            if (!ShouldEnforceOpenApiResponseContentTypes())
            {
                await WriteLegacyNegotiatedResponseAsync(inputObject, statusCode, acceptHeader);
                return;
            }

            await WriteOpenApiNegotiatedResponseAsync(inputObject, statusCode, acceptHeader);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in WriteResponseAsync");
            await WriteErrorResponseAsync("Internal server error.", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Writes a response using legacy Accept-header negotiation for non-OpenAPI routes.
    /// </summary>
    /// <param name="inputObject">The response payload.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="acceptHeader">The incoming Accept header value.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    private async Task WriteLegacyNegotiatedResponseAsync(object inputObject, int statusCode, string? acceptHeader)
    {
        var negotiated = SelectResponseMediaType(acceptHeader, LegacyNegotiatedResponseContentTypes, defaultType: "text/plain")
            ?? new ContentTypeWithSchema("text/plain", null);

        if (Logger.IsEnabled(LogEventLevel.Verbose))
        {
            Logger.Verbose(
                "Selected legacy response media type for status code: {StatusCode}, MediaType={MediaType}, Accept={Accept}",
                statusCode,
                negotiated,
                acceptHeader);
        }

        await WriteByMediaTypeAsync(negotiated.ContentType, inputObject, statusCode);
    }

    /// <summary>
    /// Writes a response using OpenAPI-declared response content types for the current status code.
    /// </summary>
    /// <param name="inputObject">The response payload.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="acceptHeader">The incoming Accept header value.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    private async Task WriteOpenApiNegotiatedResponseAsync(object inputObject, int statusCode, string? acceptHeader)
    {
        if (!TryGetResponseContentTypes(KrContext.MapRouteOptions.DefaultResponseContentType, statusCode, out var values) || values is null)
        {
            var msg = $"No default response content type configured for status code {statusCode} and no range/default fallback found.";
            Logger.Warning(msg);

            await WriteErrorResponseAsync(msg, StatusCodes.Status406NotAcceptable);
            return;
        }

        if (values.Count == 0)
        {
            var msg = $"Response status code {statusCode} is declared without content in OpenAPI. Returning a payload for this status is not allowed.";
            Logger.Warning(msg);
            await WriteErrorResponseAsync(msg, StatusCodes.Status500InternalServerError);
            return;
        }

        var supported = values as IReadOnlyList<ContentTypeWithSchema> ?? [.. values];

        var mediaType = SelectResponseMediaType(acceptHeader, supported, defaultType: supported[0].ContentType);
        if (mediaType is null)
        {
            var supportedMediaTypes = string.Join(", ", supported.Select(x => x.ContentType));
            var msg = $"No supported media type found for status code {statusCode} with Accept header '{acceptHeader}'. Supported types: {supportedMediaTypes}";
            Logger.Warning(
                "No supported media type found for status code {StatusCode} with Accept header '{AcceptHeader}'. Supported media types: {SupportedMediaTypes}. Supported entries: {@SupportedEntries}",
                statusCode,
                acceptHeader,
                supportedMediaTypes,
                supported);

            await WriteErrorResponseAsync(msg, StatusCodes.Status406NotAcceptable);
            return;
        }

        if (Logger.IsEnabled(LogEventLevel.Verbose))
        {
            Logger.Verbose(
                "Selected response media type for status code: {StatusCode}, MediaType={MediaType}, Accept={Accept}",
                statusCode,
                mediaType,
                acceptHeader);
        }

        await WriteByMediaTypeAsync(mediaType.ContentType, inputObject, statusCode);
    }

    /// <summary>
    /// Determines whether OpenAPI response content-type enforcement should run for the current route.
    /// </summary>
    /// <remarks>
    /// Enforcement is only enabled for routes that carry OpenAPI descriptive metadata.
    /// Non-OpenAPI routes continue using legacy Accept-based negotiation.
    /// </remarks>
    /// <returns>True when the route has OpenAPI metadata; otherwise false.</returns>
    private bool ShouldEnforceOpenApiResponseContentTypes() => MapRouteOptions.IsOpenApiAnnotatedFunctionRoute;

    /// <summary>
    /// Queues a response payload for deferred writing, applying configured response schema conversion and validation when available.
    /// </summary>
    /// <param name="inputObject">The payload to queue.</param>
    /// <param name="statusCode">The HTTP status code associated with the payload.</param>
    public void QueueResponseForWrite(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        if (inputObject is null)
        {
            PostPonedWriteObject = new WriteObject(null, statusCode, StatusCodes.Status500InternalServerError);
            return;
        }

        try
        {
            if (!TryGetResponseSchemaTypeForStatus(statusCode, out var schemaType, out var schemaTypeName))
            {
                PostPonedWriteObject = new WriteObject(inputObject, statusCode);
                return;
            }

            if (schemaType is null)
            {
                Logger.Error("Unable to resolve response schema type '{SchemaTypeName}' for status code {StatusCode}.", schemaTypeName, statusCode);
                PostPonedWriteObject = new WriteObject(null, statusCode, StatusCodes.Status500InternalServerError);
                return;
            }

            var inputType = inputObject.GetType();
            var valueToWrite = schemaType.IsInstanceOfType(inputObject) || inputType == schemaType
                ? inputObject
                : ConvertSchemaValue(inputObject, schemaType);

            if (!ValidateRequiredProperties(valueToWrite, out var missingProperties))
            {
                Logger.WarningSanitized(
                    "Response object failed required-property validation for schema type {SchemaTypeName}. Missing: {MissingProperties}.",
                    schemaType.FullName,
                    string.IsNullOrEmpty(missingProperties) ? "unknown required properties" : missingProperties);

                PostPonedWriteObject = new WriteObject(null, statusCode, StatusCodes.Status500InternalServerError);
                return;
            }

            PostPonedWriteObject = new WriteObject(valueToWrite, statusCode);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to convert response object for status code {StatusCode}.", statusCode);
            PostPonedWriteObject = new WriteObject(null, statusCode, StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Selects the most appropriate response media type based on the Accept header.
    /// </summary>
    /// <param name="acceptHeader">The value of the Accept header from the request.</param>
    /// <param name="supported">A list of supported media types to match against the Accept header.</param>
    /// <param name="defaultType">The default media type to use if no match is found. Defaults to "text/plain".</param>
    /// <returns>The selected media type as a string.</returns>
    /// <remarks>
    /// This method parses the Accept header, orders the media types by quality factor,
    /// and selects the first supported media type. If none are supported returns null
    /// </remarks>
    private static ContentTypeWithSchema? SelectResponseMediaType(string? acceptHeader, IReadOnlyList<ContentTypeWithSchema> supported, string defaultType = "text/plain")
    {
        if (supported.Count == 0)
        {
            return new ContentTypeWithSchema(defaultType, null);
        }

        if (string.IsNullOrWhiteSpace(acceptHeader))
        {
            return supported[0];
        }

        if (!MediaTypeHeaderValue.TryParseList([acceptHeader], out var accepts) || accepts.Count == 0)
        {
            return supported[0];
        }

        var supportsAnyMediaType = supported.Any(s => string.Equals(MediaTypeHelper.Normalize(s.ContentType), "*/*", StringComparison.OrdinalIgnoreCase));

        var supportedNormalized = new string[supported.Count];
        var supportedCanonical = new string[supported.Count];
        for (var i = 0; i < supported.Count; i++)
        {
            supportedNormalized[i] = MediaTypeHelper.Normalize(supported[i].ContentType);
            supportedCanonical[i] = MediaTypeHelper.Canonicalize(supported[i].ContentType);
        }

        foreach (var a in accepts.OrderByDescending(x => x.Quality ?? 1.0))
        {
            var accept = a.MediaType.Value;

            if (accept is null)
            {
                continue;
            }

            // Normalize first so we can reliably detect wildcards and avoid treating them as canonical aliases.
            var acceptNormalized = MediaTypeHelper.Normalize(accept);

            if (supportsAnyMediaType)
            {
                return SelectWhenAnyMediaTypeSupported(acceptNormalized, defaultType);
            }

            var matched = SelectFromConfiguredSupportedMediaTypes(acceptNormalized, supported, supportedNormalized, supportedCanonical);
            if (matched is not null)
            {
                return matched;
            }
        }
        // No match found; return default
        return null;
    }

    /// <summary>
    /// Selects a response media type when the configured supported list includes <c>*/*</c>.
    /// </summary>
    /// <param name="acceptNormalized">The normalized Accept media type.</param>
    /// <param name="defaultType">The default media type fallback.</param>
    /// <returns>The selected media type entry.</returns>
    private static ContentTypeWithSchema SelectWhenAnyMediaTypeSupported(string acceptNormalized, string defaultType)
    {
        if (string.Equals(acceptNormalized, "*/*", StringComparison.OrdinalIgnoreCase) ||
            acceptNormalized.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentTypeWithSchema(defaultType, null);
        }

        var writerMediaType = ResolveWriterMediaType(acceptNormalized, defaultType);
        return new ContentTypeWithSchema(writerMediaType, null);
    }

    /// <summary>
    /// Selects a response media type from explicitly configured supported media types.
    /// </summary>
    /// <param name="acceptNormalized">The normalized Accept media type.</param>
    /// <param name="supported">The configured supported media type entries.</param>
    /// <param name="supportedNormalized">Normalized supported media types in index-aligned order.</param>
    /// <param name="supportedCanonical">Canonical supported media types in index-aligned order.</param>
    /// <returns>The matched media type entry, or null when no match exists.</returns>
    private static ContentTypeWithSchema? SelectFromConfiguredSupportedMediaTypes(
        string acceptNormalized,
        IReadOnlyList<ContentTypeWithSchema> supported,
        IReadOnlyList<string> supportedNormalized,
        IReadOnlyList<string> supportedCanonical)
    {
        if (string.Equals(acceptNormalized, "*/*", StringComparison.OrdinalIgnoreCase))
        {
            return supported[0];
        }

        if (acceptNormalized.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = acceptNormalized[..^1]; // "application/"
            for (var i = 0; i < supported.Count; i++)
            {
                if (supportedNormalized[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return supported[i];
                }
            }

            return null;
        }

        var acceptCanonical = MediaTypeHelper.Canonicalize(acceptNormalized);

        for (var i = 0; i < supported.Count; i++)
        {
            if (string.Equals(supportedNormalized[i], acceptNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return supported[i];
            }
        }

        for (var i = 0; i < supported.Count; i++)
        {
            if (string.Equals(supportedCanonical[i], acceptCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return supported[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves an incoming Accept media type to a concrete response writer media type.
    /// </summary>
    /// <param name="acceptNormalized">The normalized Accept media type.</param>
    /// <param name="defaultType">The fallback media type.</param>
    /// <returns>A concrete media type supported by response writers.</returns>
    private static string ResolveWriterMediaType(string acceptNormalized, string defaultType)
    {
        var canonical = MediaTypeHelper.Canonicalize(acceptNormalized);
        // For common structured types with well-known suffixes, return the canonical type to ensure we return a supported media type that also supports charset parameters.
        if (string.Equals(canonical, "application/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, "application/xml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, "application/yaml", StringComparison.OrdinalIgnoreCase))
        {
            return canonical;
        }
        // Allow text/csv and application/x-www-form-urlencoded as they are commonly used text-based formats that support charset and are often expected to be returned as-is without being treated as generic text/* types.
        if (string.Equals(acceptNormalized, "text/csv", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(acceptNormalized, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return acceptNormalized;
        }
        // For other text/* types, default to text/plain since we don't want to accidentally return HTML or similar types that may have security implications.
        if (acceptNormalized.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return "text/plain";
        }
        // For other types, we would need explicit support configured to return them; default to the provided default type.
        return defaultType;
    }

    /// <summary>
    /// Resolves response content types for a status code using exact, range (e.g., 4XX), then default.
    /// </summary>
    /// <param name="contentTypes">The content type map keyed by status code, range, or default.</param>
    /// <param name="statusCode">The HTTP status code to resolve.</param>
    /// <param name="values">The resolved content types, if found.</param>
    /// <returns>True when a matching entry is found and contains at least one value.</returns>
    private static bool TryGetResponseContentTypes(
        IDictionary<string, ICollection<ContentTypeWithSchema>>? contentTypes,
        int statusCode,
        out ICollection<ContentTypeWithSchema>? values)
    {
        values = null;
        if (contentTypes is null || contentTypes.Count == 0)
        {
            return false;
        }

        var statusKey = statusCode.ToString(CultureInfo.InvariantCulture);
        if (TryGetValueIgnoreCase(contentTypes, statusKey, out values))
        {
            return true;
        }

        if (statusCode is >= 100 and <= 599)
        {
            // Allow OpenAPI-style wildcard keys such as:
            // - 4XX (all 4xx)
            // These are matched case-insensitively.
            var rangeKey = $"{statusCode / 100}XX";
            if (TryGetValueIgnoreCase(contentTypes, rangeKey, out values) && values is { Count: > 0 })
            {
                return true;
            }
        }

        if (TryGetValueIgnoreCase(contentTypes, "default", out values) && values is { Count: > 0 })
        {
            return true;
        }

        values = null;
        return false;
    }

    /// <summary>
    /// Attempts to resolve a configured response schema type for the given status code.
    /// </summary>
    /// <param name="statusCode">The status code for which a schema should be resolved.</param>
    /// <param name="schemaType">The resolved schema type, when available.</param>
    /// <param name="schemaTypeName">The configured schema type name.</param>
    /// <returns>True when a schema mapping exists for the status code and includes schema metadata.</returns>
    private bool TryGetResponseSchemaTypeForStatus(int statusCode, out Type? schemaType, out string? schemaTypeName)
    {
        schemaType = null;
        schemaTypeName = null;

        if (!TryGetResponseContentTypes(MapRouteOptions.DefaultResponseContentType, statusCode, out var mappings) || mappings is null || mappings.Count == 0)
        {
            return false;
        }

        var first = mappings.FirstOrDefault();
        if (first is null || string.IsNullOrWhiteSpace(first.Schema))
        {
            return false;
        }

        schemaTypeName = first.Schema;
        schemaType = ResolveSchemaType(schemaTypeName);
        return true;
    }

    /// <summary>
    /// Resolves a type by full name, short name, or assembly-qualified name from loaded assemblies.
    /// </summary>
    /// <param name="schemaTypeName">The schema type name to resolve.</param>
    /// <returns>The resolved type when found; otherwise null.</returns>
    private static Type? ResolveSchemaType(string schemaTypeName)
    {
        if (string.IsNullOrWhiteSpace(schemaTypeName))
        {
            return null;
        }

        var candidates = new List<Type>();

        var directType = Type.GetType(schemaTypeName, throwOnError: false, ignoreCase: true);
        if (directType is not null)
        {
            candidates.Add(directType);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            CollectSchemaTypeCandidatesFromAssembly(assembly, schemaTypeName, candidates);
        }

        return SelectPreferredSchemaType(candidates);
    }

    /// <summary>
    /// Collects schema type candidates from an assembly by direct and name-based matching.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="schemaTypeName">The schema type name to resolve.</param>
    /// <param name="candidates">The destination list of matching candidates.</param>
    private static void CollectSchemaTypeCandidatesFromAssembly(Assembly assembly, string schemaTypeName, List<Type> candidates)
    {
        var assemblyType = assembly.GetType(schemaTypeName, throwOnError: false, ignoreCase: true);
        if (assemblyType is not null)
        {
            candidates.Add(assemblyType);
        }

        Type[] typeCandidates;
        try
        {
            typeCandidates = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            typeCandidates = [.. ex.Types.Where(t => t is not null)!];
        }
        catch
        {
            return;
        }

        foreach (var typeCandidate in typeCandidates)
        {
            if (typeCandidate is not null && IsMatchingSchemaTypeName(typeCandidate, schemaTypeName))
            {
                candidates.Add(typeCandidate);
            }
        }
    }

    /// <summary>
    /// Determines whether a candidate type matches the provided schema type name.
    /// </summary>
    /// <param name="typeCandidate">The type candidate to evaluate.</param>
    /// <param name="schemaTypeName">The schema type name to match.</param>
    /// <returns>True when the candidate matches by full name, short name, or assembly-qualified prefix.</returns>
    private static bool IsMatchingSchemaTypeName(Type typeCandidate, string schemaTypeName)
        => string.Equals(typeCandidate.FullName, schemaTypeName, StringComparison.OrdinalIgnoreCase)
           || string.Equals(typeCandidate.Name, schemaTypeName, StringComparison.OrdinalIgnoreCase)
           || (!string.IsNullOrWhiteSpace(typeCandidate.AssemblyQualifiedName)
               && typeCandidate.AssemblyQualifiedName.StartsWith(schemaTypeName + ",", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Selects the preferred schema type from collected candidates.
    /// </summary>
    /// <param name="candidates">The collected type candidates.</param>
    /// <returns>The preferred schema type, or null when no candidate exists.</returns>
    private static Type? SelectPreferredSchemaType(IReadOnlyList<Type> candidates)
    {
        var distinct = candidates
            .Where(t => t is not null)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.AssemblyQualifiedName) ? t.FullName : t.AssemblyQualifiedName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (distinct.Count == 0)
        {
            return null;
        }

        var generatedCandidate = distinct.FirstOrDefault(t =>
            t.GetProperty("XmlMetadata", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) is not null);

        return generatedCandidate ?? distinct[0];
    }

    /// <summary>
    /// Converts a value to the target schema type.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertSchemaValue(object? value, Type targetType)
    {
        value = UnwrapPowerShellValue(value);
        if (value is null)
        {
            return null;
        }
        // If the value is already assignable to the target type, return it directly without further conversion.
        var valueType = value.GetType();
        if (targetType.IsInstanceOfType(value) || valueType == targetType)
        {
            return value;
        }
        // Handle nullable types by converting to the underlying type.
        var nullableUnderlying = Nullable.GetUnderlyingType(targetType);
        if (nullableUnderlying is not null)
        {
            return ConvertSchemaValue(value, nullableUnderlying);
        }
        // If the target type is a map-like type (e.g., IDictionary) and the value is an IDictionary, return it directly to allow for flexible dictionary handling in schemas without forcing unnecessary conversions that may not be compatible with all dictionary types.
        if (IsMapLikeType(targetType) && value is IDictionary)
        {
            return value;
        }
        // If the target type is an array, attempt to convert the value to an array of the target element type. This handles cases where the schema expects an array but the provided value may be a single item or a different enumerable type.
        if (targetType.IsArray)
        {
            return ConvertSchemaArrayValue(value, targetType);
        }
        // Attempt dictionary-to-type conversion for schema values, which allows for flexible object construction from dictionaries when the schema type is a complex object. This is attempted before PowerShell object conversion to prioritize direct dictionary mappings for schemas that may be designed to be populated from dictionaries.
        if (TryConvertSchemaDictionaryValue(value, targetType, out var dictionaryConvertedValue))
        {
            return dictionaryConvertedValue;
        }
        // Attempt PowerShell object conversion, which allows for flexible handling of PowerShell-specific objects and types that may be passed as response values. This is attempted after dictionary conversion to allow schemas that can be populated from dictionaries to take precedence, while still supporting PowerShell object conversion when needed for schema types that may not be directly compatible with dictionary conversion.
        if (TryConvertPowerShellObjectToType(value, targetType, out var convertedPowerShellObject))
        {
            return convertedPowerShellObject;
        }
        // Attempt single-argument constructor conversion as a last resort before simple conversions, as it may be more expensive and we want to prioritize more direct conversions when possible.
        if (TryConvertViaSingleArgumentConstructor(value, targetType, out var constructorConvertedValue))
        {
            return constructorConvertedValue;
        }
        // As a final fallback, attempt simple conversions for primitive types and common convertible types, which allows for some flexibility in converting between basic types when the schema expects a different type but a simple conversion is possible (e.g., string to int). This is attempted last to allow all other more specific conversion strategies to take precedence, while still providing a fallback for simple type conversions that may be commonly needed in schemas.
        return TryConvertSimple(value, targetType, out var convertedValue) ? convertedValue : value;
    }

    /// <summary>
    /// Converts a schema value to an array of the requested target type.
    /// </summary>
    /// <param name="value">The source value to convert.</param>
    /// <param name="targetArrayType">The destination array type.</param>
    /// <returns>A typed array instance populated from the source value.</returns>
    private static object ConvertSchemaArrayValue(object value, Type targetArrayType)
    {
        var elementType = targetArrayType.GetElementType() ?? typeof(object);
        if (value is IEnumerable enumerable and not string)
        {
            return ConvertEnumerableToTypedArray(enumerable, elementType);
        }
        // If the value is not an enumerable, attempt to convert it as a single element array.
        return ConvertSingleValueToTypedArray(value, elementType);
    }

    /// <summary>
    /// Converts an enumerable source to a typed destination array.
    /// </summary>
    /// <param name="enumerable">The source enumerable values.</param>
    /// <param name="elementType">The destination element type.</param>
    /// <returns>A typed array containing converted elements.</returns>
    private static Array ConvertEnumerableToTypedArray(IEnumerable enumerable, Type elementType)
    {
        var materialized = new List<object?>();
        foreach (var item in enumerable)
        {
            materialized.Add(ConvertSchemaValue(item, elementType));
        }
        // Create a typed array of the destination element type and populate it with the converted values, ensuring that each element is assignable to the destination element type. This allows for flexible conversion of enumerable values to arrays of the expected schema element type, while still enforcing that the final array contains compatible element types as required by the schema.
        var typedArray = Array.CreateInstance(elementType, materialized.Count);
        for (var i = 0; i < materialized.Count; i++)
        {
            var itemToAssign = EnsureArrayElementAssignable(materialized[i], elementType, allowSimpleFallback: false);
            typedArray.SetValue(itemToAssign, i);
        }
        // Return the fully converted and typed array to be used as the response value, which ensures that the response adheres to the expected schema-defined array type with properly converted elements.
        return typedArray;
    }

    /// <summary>
    /// Converts a single source value to a one-element typed destination array.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="elementType">The destination element type.</param>
    /// <returns>A one-element typed array containing the converted value.</returns>
    private static Array ConvertSingleValueToTypedArray(object value, Type elementType)
    {
        var singleItemArray = Array.CreateInstance(elementType, 1);
        var singleElement = EnsureArrayElementAssignable(ConvertSchemaValue(value, elementType), elementType, allowSimpleFallback: true);
        singleItemArray.SetValue(singleElement, 0);
        return singleItemArray;
    }

    /// <summary>
    /// Ensures that an array element is assignable to the destination element type.
    /// </summary>
    /// <param name="candidate">The candidate value to assign.</param>
    /// <param name="elementType">The required destination element type.</param>
    /// <param name="allowSimpleFallback">Whether to attempt simple conversion before throwing.</param>
    /// <returns>A value assignable to the destination element type, or null.</returns>
    /// <exception cref="InvalidCastException">Thrown when conversion to the destination element type fails.</exception>
    private static object? EnsureArrayElementAssignable(object? candidate, Type elementType, bool allowSimpleFallback)
    {
        var unwrapped = UnwrapPowerShellValue(candidate);
        if (unwrapped is null || elementType.IsInstanceOfType(unwrapped))
        {
            return unwrapped;
        }

        // Attempt schema conversion on the element to ensure it is compatible with the destination element type, which allows for flexible handling of array elements that may require conversion to match the schema-defined element type. This is done before simple fallback conversions to prioritize proper schema conversions for array elements, while still allowing for a simple conversion fallback when appropriate for single value to array conversions.
        var convertedElement = UnwrapPowerShellValue(ConvertSchemaValue(unwrapped, elementType));
        if (convertedElement is null || elementType.IsInstanceOfType(convertedElement))
        {
            return convertedElement;
        }

        // If allowed, attempt a simple conversion as a final fallback before throwing, which provides some leniency for converting single values to array element types when the value is not directly assignable but a simple conversion may succeed (e.g., string to int). This is only attempted for single value to array conversions, not for enumerable conversions, to avoid unintended consequences of applying simple conversions to each element in an enumerable when the source value is already an enumerable that failed element-wise conversion.
        if (allowSimpleFallback &&
            TryConvertSimple(convertedElement, elementType, out var simpleConverted) &&
            (simpleConverted is null || elementType.IsInstanceOfType(simpleConverted)))
        {
            return simpleConverted;
        }

        // If the element cannot be converted to the required type, throw an exception to indicate a schema validation failure. This ensures that we don't silently produce arrays with incompatible element types that may cause issues later on when the array is used.
        throw new InvalidCastException($"Object of type '{convertedElement.GetType().FullName}' cannot be converted to '{elementType.FullName}'.");
    }

    /// <summary>
    /// Attempts dictionary-to-type conversion for schema values.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="convertedValue">The converted value when successful.</param>
    /// <returns>True when dictionary conversion succeeds; otherwise false.</returns>
    private static bool TryConvertSchemaDictionaryValue(object value, Type targetType, out object? convertedValue)
    {
        convertedValue = null;
        if (value is not IDictionary dictionary)
        {
            return false;
        }

        var (success, convertedDictionaryValue) = TryConvertDictionaryToType(dictionary, targetType);
        if (!success)
        {
            return false;
        }

        convertedValue = convertedDictionaryValue;
        return true;
    }

    /// <summary>
    /// Attempts to convert a value to the target type by using single-argument constructors.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="convertedValue">The constructed value when successful.</param>
    /// <returns>True when a single-argument constructor conversion succeeds; otherwise false.</returns>
    private static bool TryConvertViaSingleArgumentConstructor(object value, Type targetType, out object? convertedValue)
    {
        convertedValue = null;
        foreach (var constructor in targetType.GetConstructors().Where(c => c.GetParameters().Length == 1))
        {
            var parameterType = constructor.GetParameters()[0].ParameterType;
            if (!TryConvertSimple(value, parameterType, out var convertedArg))
            {
                continue;
            }

            convertedValue = constructor.Invoke([convertedArg]);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unwraps common PowerShell wrapper/sentinel values into .NET runtime values.
    /// </summary>
    /// <param name="value">The value to unwrap.</param>
    /// <returns>The unwrapped value, or null for AutomationNull.</returns>
    private static object? UnwrapPowerShellValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (IsPowerShellAutomationNull(value))
        {
            return null;
        }

        if (value is PSObject psObject)
        {
            var baseObject = psObject.BaseObject;
            return baseObject is null || IsPowerShellAutomationNull(baseObject)
                ? null
                : baseObject;
        }

        return value;
    }

    /// <summary>
    /// Determines whether a value represents PowerShell's AutomationNull sentinel.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>True when the value is PowerShell AutomationNull.</returns>
    private static bool IsPowerShellAutomationNull(object value)
    {
        var type = value.GetType();
        return type.FullName?.Equals("System.Management.Automation.Internal.AutomationNull", StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Attempts to convert dictionary values to a strongly typed object.
    /// </summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns>A tuple indicating conversion success and converted value.</returns>
    private static (bool Success, object? Value) TryConvertDictionaryToType(IDictionary dictionary, Type targetType)
    {
        var defaultConstructor = targetType.GetConstructor(Type.EmptyTypes);
        if (defaultConstructor is null)
        {
            return (false, null);
        }

        var instance = defaultConstructor.Invoke([]);
        var writableProperties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        foreach (var property in writableProperties)
        {
            var matchKey = FindDictionaryKey(dictionary, property.Name);
            if (matchKey is null)
            {
                continue;
            }

            var rawValue = dictionary[matchKey];
            if (rawValue is IDictionary && IsMapLikeType(property.PropertyType))
            {
                return (true, dictionary);
            }

            var convertedPropertyValue = ConvertSchemaValue(rawValue, property.PropertyType);
            property.SetValue(instance, convertedPropertyValue);
        }

        return (true, instance);
    }

    /// <summary>
    /// Attempts to convert a PowerShell custom object to a strongly typed object by mapping note properties.
    /// </summary>
    /// <param name="value">The source PowerShell object.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="converted">The converted object when successful.</param>
    /// <returns>True when conversion succeeds.</returns>
    private static bool TryConvertPowerShellObjectToType(object value, Type targetType, out object? converted)
    {
        converted = null;

        var typeName = value.GetType().FullName;
        if (!string.Equals(typeName, "System.Management.Automation.PSCustomObject", StringComparison.Ordinal))
        {
            return false;
        }

        var asPsObject = value as PSObject ?? new PSObject(value);
        var dictionary = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (var property in asPsObject.Properties)
        {
            if (property is null || string.IsNullOrWhiteSpace(property.Name))
            {
                continue;
            }

            dictionary[property.Name] = property.Value;
        }

        var (success, convertedValue) = TryConvertDictionaryToType(dictionary, targetType);
        if (!success)
        {
            return false;
        }

        converted = convertedValue;
        return true;
    }

    /// <summary>
    /// Finds a dictionary key by case-insensitive string comparison.
    /// </summary>
    /// <param name="dictionary">The dictionary to search.</param>
    /// <param name="propertyName">The property name to match.</param>
    /// <returns>The matching dictionary key, if found.</returns>
    private static object? FindDictionaryKey(IDictionary dictionary, string propertyName)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is null)
            {
                continue;
            }

            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates required properties using generated helper methods when available.
    /// </summary>
    /// <param name="value">The converted value to validate.</param>
    /// <param name="missingProperties">A comma-separated list of missing properties.</param>
    /// <returns>True when validation succeeds or no validation helper exists.</returns>
    private static bool ValidateRequiredProperties(object? value, out string missingProperties)
    {
        missingProperties = string.Empty;
        if (value is null)
        {
            return true;
        }

        var runtimeType = value.GetType();
        var validateMethod = runtimeType.GetMethod("ValidateRequiredProperties", BindingFlags.Public | BindingFlags.Instance);
        if (validateMethod is null)
        {
            return true;
        }

        var validationResult = validateMethod.Invoke(value, null);
        if (validationResult is bool isValid && isValid)
        {
            return true;
        }

        var missingMethod = runtimeType.GetMethod("GetMissingRequiredProperties", BindingFlags.Public | BindingFlags.Instance);
        if (missingMethod is not null)
        {
            var missing = missingMethod.Invoke(value, null);
            missingProperties = FormatMissingRequiredProperties(missing);
        }

        return false;
    }

    /// <summary>
    /// Formats missing required-property values returned by generated validation helpers.
    /// </summary>
    /// <param name="missing">The missing-properties payload returned by reflection invocation.</param>
    /// <returns>A comma-separated string of missing property names, or an empty string when unavailable.</returns>
    private static string FormatMissingRequiredProperties(object? missing)
    {
        if (missing is IEnumerable<string> missingEnumerable)
        {
            return string.Join(", ", missingEnumerable);
        }

        if (missing is IEnumerable genericEnumerable)
        {
            var values = new List<string>();
            foreach (var item in genericEnumerable)
            {
                values.Add(item?.ToString() ?? string.Empty);
            }

            return string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines whether a type should be treated as map-like for dictionary passthrough.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True when the type is map-like.</returns>
    private static bool IsMapLikeType(Type type)
    {
        if (type.GetProperty("AdditionalProperties", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy) is not null)
        {
            return true;
        }

        var attributes = type.GetCustomAttributes(inherit: true);
        foreach (var attribute in attributes)
        {
            if (attribute.GetType().Name.Equals("OpenApiPatternPropertiesAttribute", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to perform basic runtime conversions.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="converted">The converted value when successful.</param>
    /// <returns>True when conversion succeeds.</returns>
    private static bool TryConvertSimple(object? value, Type targetType, out object? converted)
    {
        converted = null;
        if (value is null)
        {
            return false;
        }

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
        {
            converted = value;
            return true;
        }

        try
        {
            if (targetType.IsEnum)
            {
                converted = value is string s
                    ? Enum.Parse(targetType, s, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
                return true;
            }

            converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            try
            {
                converted = LanguagePrimitives.ConvertTo(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Attempts to read a dictionary value with case-insensitive key matching.
    /// </summary>
    /// <typeparam name="T">The dictionary value type.</typeparam>
    /// <param name="dict">The dictionary to read from.</param>
    /// <param name="key">The key to search for.</param>
    /// <param name="value">The matched value, if found.</param>
    /// <returns>True when a matching key is found.</returns>
    private static bool TryGetValueIgnoreCase<T>(IDictionary<string, T> dict, string key, out T? value)
    {
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Writes a response based on the specified media type.
    /// </summary>
    /// <param name="mediaType">The media type to use for the response.</param>
    /// <param name="inputObject">The object to be written in the response body.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private Task WriteByMediaTypeAsync(string mediaType, object? inputObject, int statusCode)
    {
        // If you want, set Response.ContentType here once, centrally.
        ContentType = mediaType;

        return mediaType switch
        {
            "application/json" => WriteJsonResponseAsync(inputObject, statusCode, mediaType),
            "application/yaml" => WriteYamlResponseAsync(inputObject, statusCode, mediaType),
            "application/xml" => WriteXmlResponseAsync(inputObject, statusCode, mediaType),
            "application/bson" => WriteBsonResponseAsync(inputObject, statusCode, mediaType),
            "application/cbor" => WriteCborResponseAsync(inputObject, statusCode, mediaType),
            "text/csv" => WriteCsvResponseAsync(inputObject, statusCode, mediaType),
            "application/x-www-form-urlencoded" => WriteFormUrlEncodedResponseAsync(inputObject, statusCode),
            _ => WriteTextResponseAsync(inputObject?.ToString() ?? string.Empty, statusCode),
        };
    }

    /// <summary>
    /// Writes a CSV response with the specified input object, status code, content type, and optional CsvConfiguration.
    /// </summary>
    /// <param name="inputObject">The object to be converted to CSV.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="config">An optional CsvConfiguration to customize CSV output.</param>
    public void WriteCsvResponse(
            object? inputObject,
            int statusCode = StatusCodes.Status200OK,
            string? contentType = null,
            CsvConfiguration? config = null)
    {
        Action<CsvConfiguration>? tweaker = null;

        if (config is not null)
        {
            tweaker = target =>
            {
                foreach (var prop in typeof(CsvConfiguration)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        var value = prop.GetValue(config);
                        prop.SetValue(target, value);
                    }
                }
            };
        }
        WriteCsvResponseAsync(inputObject, statusCode, contentType, tweaker).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously writes a CSV response with the specified input object, status code, content type, and optional configuration tweak.
    /// </summary>
    /// <param name="inputObject">The object to be converted to CSV.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="tweak">An optional action to tweak the CsvConfiguration.</param>
    public async Task WriteCsvResponseAsync(
        object? inputObject,
        int statusCode = StatusCodes.Status200OK,
        string? contentType = null,
        Action<CsvConfiguration>? tweak = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing CSV response (async), StatusCode={StatusCode}, ContentType={ContentType}",
                      statusCode, contentType);
        }

        // Serialize inside a background task so heavy reflection never blocks the caller
        Body = await Task.Run(() =>
        {
            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                NewLine = Environment.NewLine
            };
            tweak?.Invoke(cfg);                         // let the caller flirt with the config

            using var sw = new StringWriter();
            using var csv = new CsvWriter(sw, cfg);

            // CsvHelper insists on an enumerable; wrap single objects so it stays happy
            if (inputObject is IEnumerable records and not string)
            {
                csv.WriteRecords(records);              // whole collections (IEnumerable<T>)
            }
            else if (inputObject is not null)
            {
                csv.WriteRecords([inputObject]); // lone POCO
            }
            else
            {
                csv.WriteHeader<object>();              // nothing? write only headers for an empty file
            }

            return sw.ToString();
        }).ConfigureAwait(false);

        ContentType = string.IsNullOrEmpty(contentType)
            ? $"text/csv; charset={Encoding.WebName}"
            : contentType;
        StatusCode = statusCode;
    }
    /// <summary>
    /// Writes a YAML response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to YAML.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteYamlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteYamlResponseAsync(inputObject, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a YAML response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to YAML.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteYamlResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing YAML response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        Body = await Task.Run(() => YamlHelper.ToYaml(inputObject));
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/yaml; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Writes an XML response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to XML.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="rootElementName">Optional custom XML root element name. Defaults to <c>Response</c>.</param>
    /// <param name="compress">If true, emits compact XML (no indentation); if false (default) output is human readable.</param>
    public void WriteXmlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null, string? rootElementName = null, bool compress = false)
        => WriteXmlResponseAsync(inputObject, statusCode, contentType, rootElementName, compress).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes an XML response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to XML.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="rootElementName">Optional custom XML root element name. Defaults to <c>Response</c>.</param>
    /// <param name="compress">If true, emits compact XML (no indentation); if false (default) output is human readable.</param>
    public async Task WriteXmlResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null, string? rootElementName = null, bool compress = false)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing XML response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        var root = string.IsNullOrWhiteSpace(rootElementName) ? "Response" : rootElementName.Trim();
        var xml = await Task.Run(() => XmlHelper.ToXml(root, inputObject));
        var saveOptions = compress ? SaveOptions.DisableFormatting : SaveOptions.None;
        Body = await Task.Run(() => xml.ToString(saveOptions));
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/xml; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }
    /// <summary>
    /// Writes a text response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to a text response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteTextResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) =>
        WriteTextResponseAsync(inputObject, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a text response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to a text response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteTextResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing text response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        if (inputObject is null)
        {
            throw new ArgumentNullException(nameof(inputObject), "Input object cannot be null for text response.");
        }

        Body = await Task.Run(() => inputObject?.ToString() ?? string.Empty);
        ContentType = string.IsNullOrEmpty(contentType) ? $"text/plain; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Writes a form-urlencoded response with the specified input object, status code, and optional content type.
    /// Automatically converts the input object to a Dictionary{string, string} using <see cref="ObjectToDictionaryConverter"/>.
    /// </summary>
    /// <param name="inputObject">The object to be converted to form-urlencoded data. Can be a dictionary, enumerable, or any object with public properties.</param>
    /// <param name="statusCode">The HTTP status code for the response. Defaults to 200 OK.</param>
    public void WriteFormUrlEncodedResponse(object? inputObject, int statusCode = StatusCodes.Status200OK) =>
        WriteFormUrlEncodedResponseAsync(inputObject, statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a form-urlencoded response with the specified input object, status code, and optional content type.
    /// Automatically converts the input object to a Dictionary{string, string} using <see cref="ObjectToDictionaryConverter"/>.
    /// </summary>
    /// <param name="inputObject">The object to be converted to form-urlencoded data. Can be a dictionary, enumerable, or any object with public properties.</param>
    /// <param name="statusCode">The HTTP status code for the response. Defaults to 200 OK.</param>
    public async Task WriteFormUrlEncodedResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        if (inputObject is null)
        {
            throw new ArgumentNullException(nameof(inputObject), "Input object cannot be null for form-urlencoded response.");
        }

        var dictionary = ObjectToDictionaryConverter.ToDictionary(inputObject);
        var formContent = new FormUrlEncodedContent(dictionary);
        var encodedString = await formContent.ReadAsStringAsync();

        await WriteTextResponseAsync(encodedString, statusCode, "application/x-www-form-urlencoded");
    }

    /// <summary>
    /// Writes an HTTP redirect response with the specified URL and optional message.
    /// </summary>
    /// <param name="url">The URL to redirect to.</param>
    /// <param name="message">An optional message to include in the response body.</param>
    public void WriteRedirectResponse(string url, string? message = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing redirect response, StatusCode={StatusCode}, Location={Location}", StatusCode, url);
        }

        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url), "URL cannot be null for redirect response.");
        }
        // framework hook
        RedirectUrl = url;

        // HTTP status + Location header
        StatusCode = StatusCodes.Status302Found;
        Headers["Location"] = url;

        if (message is not null)
        {
            // include a body
            Body = message;
            ContentType = $"text/plain; charset={Encoding.WebName}";
        }
        else
        {
            // no body: clear any existing body/headers
            Body = null;
            //ContentType = null;
            _ = Headers.Remove("Content-Length");
        }
    }

    /// <summary>
    /// Writes a binary response with the specified data, status code, and content type.
    /// </summary>
    /// <param name="data">The binary data to send in the response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteBinaryResponse(byte[] data, int statusCode = StatusCodes.Status200OK, string contentType = "application/octet-stream")
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing binary response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        Body = data ?? throw new ArgumentNullException(nameof(data), "Data cannot be null for binary response.");
        ContentType = contentType;
        StatusCode = statusCode;
    }
    /// <summary>
    /// Writes a stream response with the specified stream, status code, and content type.
    /// </summary>
    /// <param name="stream">The stream to send in the response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteStreamResponse(Stream stream, int statusCode = StatusCodes.Status200OK, string contentType = "application/octet-stream")
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing stream response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        Body = stream;
        ContentType = contentType;
        StatusCode = statusCode;
    }
    #endregion

    #region Error Responses
    /// <summary>
    /// Structured payload for error responses.
    /// </summary>
    internal record ErrorPayload
    {
        public string Error { get; init; } = default!;
        public string? Details { get; init; }
        public string? Exception { get; init; }
        public string? StackTrace { get; init; }
        public int Status { get; init; }
        public string Reason { get; init; } = default!;
        public string Timestamp { get; init; } = default!;
        public string? Path { get; init; }
        public string? Method { get; init; }
    }

    /// <summary>
    /// Write an error response with a custom message.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    public async Task WriteErrorResponseAsync(
        string message,
        int statusCode = StatusCodes.Status500InternalServerError,
        string? contentType = null,
        string? details = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing error response, StatusCode={StatusCode}, ContentType={ContentType}, Message={Message}",
                statusCode, contentType, message);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentNullException(nameof(message));
        }

        Logger.Warning("Writing error response with status {StatusCode}: {Message}", statusCode, message);

        var payload = new ErrorPayload
        {
            Error = message,
            Details = details,
            Exception = null,
            StackTrace = null,
            Status = statusCode,
            Reason = ReasonPhrases.GetReasonPhrase(statusCode),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Path = Request?.Path,
            Method = Request?.Method
        };

        await WriteFormattedErrorResponseAsync(payload, contentType);
    }

    /// <summary>
    /// Writes an error response with a custom message.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    /// <param name="message">The error message to include in the response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="details">Optional details to include in the response.</param>
    public void WriteErrorResponse(
      string message,
      int statusCode = StatusCodes.Status500InternalServerError,
      string? contentType = null,
      string? details = null) => WriteErrorResponseAsync(message, statusCode, contentType, details).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes an error response based on an exception.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    /// <param name="ex">The exception to report.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="includeStack">Whether to include the stack trace in the response.</param>
    public async Task WriteErrorResponseAsync(
        Exception ex,
        int statusCode = StatusCodes.Status500InternalServerError,
        string? contentType = null,
        bool includeStack = true)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing error response from exception, StatusCode={StatusCode}, ContentType={ContentType}, IncludeStack={IncludeStack}",
                statusCode, contentType, includeStack);
        }

        ArgumentNullException.ThrowIfNull(ex);

        Logger.Warning(ex, "Writing error response with status {StatusCode}", statusCode);

        var payload = new ErrorPayload
        {
            Error = ex.Message,
            Details = null,
            Exception = ex.GetType().Name,
            StackTrace = includeStack ? ex.ToString() : null,
            Status = statusCode,
            Reason = ReasonPhrases.GetReasonPhrase(statusCode),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Path = Request?.Path,
            Method = Request?.Method
        };

        await WriteFormattedErrorResponseAsync(payload, contentType);
    }
    /// <summary>
    /// Writes an error response based on an exception.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    /// <param name="ex">The exception to report.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    /// <param name="includeStack">Whether to include the stack trace in the response.</param>
    public void WriteErrorResponse(
            Exception ex,
            int statusCode = StatusCodes.Status500InternalServerError,
            string? contentType = null,
            bool includeStack = true) => WriteErrorResponseAsync(ex, statusCode, contentType, includeStack).GetAwaiter().GetResult();

    /// <summary>
    /// Internal dispatcher: serializes the payload according to the chosen content-type.
    /// </summary>
    private async Task WriteFormattedErrorResponseAsync(ErrorPayload payload, string? contentType = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing formatted error response, ContentType={ContentType}, Status={Status}", contentType, payload.Status);
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            _ = Request.Headers.TryGetValue("Accept", out var acceptHeader);
            contentType = (acceptHeader ?? "text/plain")
                                 .ToLowerInvariant();
        }
        if (contentType.Contains("json"))
        {
            await WriteJsonResponseAsync(payload, payload.Status);
        }
        else if (contentType.Contains("yaml") || contentType.Contains("yml"))
        {
            await WriteYamlResponseAsync(payload, payload.Status);
        }
        else if (contentType.Contains("xml"))
        {
            await WriteXmlResponseAsync(payload, payload.Status);
        }
        else
        {
            // Plain-text fallback
            var lines = new List<string>
                {
                    $"Status: {payload.Status} ({payload.Reason})",
                    $"Error: {payload.Error}",
                    $"Time: {payload.Timestamp}"
                };

            if (!string.IsNullOrWhiteSpace(payload.Details))
            {
                lines.Add("Details:\n" + payload.Details);
            }

            if (!string.IsNullOrWhiteSpace(payload.Exception))
            {
                lines.Add($"Exception: {payload.Exception}");
            }

            if (!string.IsNullOrWhiteSpace(payload.StackTrace))
            {
                lines.Add("StackTrace:\n" + payload.StackTrace);
            }

            var text = string.Join("\n", lines);
            await WriteTextResponseAsync(text, payload.Status, "text/plain");
        }
    }

    #endregion
    #region HTML Response Helpers

    /// <summary>
    /// Renders a template string by replacing placeholders in the format {{key}} with corresponding values from the provided dictionary.
    /// </summary>
    /// <param name="template">The template string containing placeholders.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <returns>The rendered string with placeholders replaced by variable values.</returns>
    private string RenderInlineTemplate(
     string template,
     IReadOnlyDictionary<string, object?> vars)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Rendering inline template, TemplateLength={TemplateLength}, VarsCount={VarsCount}",
                      template?.Length ?? 0, vars?.Count ?? 0);
        }

        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (vars is null || vars.Count == 0)
        {
            return template;
        }

        var render = RenderInline(template, vars);

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Rendered template length: {RenderedLength}", render.Length);
        }

        return render;
    }

    /// <summary>
    /// Renders a template string by replacing placeholders in the format {{key}} with corresponding values from the provided dictionary.
    /// </summary>
    /// <param name="template">The template string containing placeholders.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <returns>The rendered string with placeholders replaced by variable values.</returns>
    private static string RenderInline(string template, IReadOnlyDictionary<string, object?> vars)
    {
        var sb = new StringBuilder(template.Length);

        // Iterate through the template
        var i = 0;
        while (i < template.Length)
        {
            // opening “{{”
            if (template[i] == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                var start = i + 2;                                        // after “{{”
                var end = template.IndexOf("}}", start, StringComparison.Ordinal);

                if (end > start)                                          // found closing “}}”
                {
                    var rawKey = template[start..end].Trim();

                    if (TryResolveValue(rawKey, vars, out var value) && value is not null)
                    {
                        _ = sb.Append(value); // append resolved value
                    }
                    else
                    {
                        _ = sb.Append("{{").Append(rawKey).Append("}}");      // leave it as-is if unknown
                    }

                    i = end + 2;    // jump past the “}}”
                    continue;
                }
            }

            // ordinary character
            _ = sb.Append(template[i]);
            i++; // move to the next character
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resolves a dotted path like “Request.Path” through nested dictionaries
    /// and/or object properties (case-insensitive).
    /// </summary>
    private static bool TryResolveValue(
        string path,
        IReadOnlyDictionary<string, object?> root,
        out object? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        object? current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
            {
                return false;
            }

            // ① Handle dictionary look-ups (IReadOnlyDictionary or IDictionary)
            if (current is IReadOnlyDictionary<string, object?> roDict)
            {
                if (!roDict.TryGetValue(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current is IDictionary dict)
            {
                if (!dict.Contains(segment))
                {
                    return false;
                }

                current = dict[segment];
                continue;
            }

            // ② Handle property look-ups via reflection
            var prop = current.GetType().GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null)
            {
                return false;
            }

            current = prop.GetValue(current);
        }

        value = current;
        return true;
    }

    /// <summary>
    /// Attempts to revalidate the cache based on ETag and Last-Modified headers.
    /// If the resource is unchanged, sets the response status to 304 Not Modified.
    /// Returns true if a 304 response was written, false otherwise.
    /// </summary>
    /// <param name="payload">The payload to validate.</param>
    /// <param name="etag">The ETag header value.</param>
    /// <param name="weakETag">Indicates if the ETag is a weak ETag.</param>
    /// <param name="lastModified">The Last-Modified header value.</param>
    /// <returns>True if a 304 response was written, false otherwise.</returns>
    public bool RevalidateCache(object? payload,
       string? etag = null,
       bool weakETag = false,
       DateTimeOffset? lastModified = null) => CacheRevalidation.TryWrite304(Context, payload, etag, weakETag, lastModified);

    /// <summary>
    /// Asynchronously writes an HTML response, rendering the provided template string and replacing placeholders with values from the given dictionary.
    /// </summary>
    /// <param name="template">The HTML template string containing placeholders.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public async Task WriteHtmlResponseAsync(
        string template,
        IReadOnlyDictionary<string, object?>? vars,
        int statusCode = 200)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing HTML response (async), StatusCode={StatusCode}, TemplateLength={TemplateLength}", statusCode, template.Length);
        }

        if (vars is null || vars.Count == 0)
        {
            await WriteTextResponseAsync(template, statusCode, "text/html");
        }
        else
        {
            await WriteTextResponseAsync(RenderInlineTemplate(template, vars), statusCode, "text/html");
        }
    }

    /// <summary>
    /// Asynchronously writes an HTML response, rendering the provided template byte array and replacing placeholders with values from the given dictionary.
    /// </summary>
    /// <param name="template">The HTML template byte array.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteHtmlResponseAsync(
    byte[] template,
    IReadOnlyDictionary<string, object?>? vars,
    int statusCode = 200) => await WriteHtmlResponseAsync(Encoding.GetString(template), vars, statusCode);

    /// <summary>
    /// Writes an HTML response, rendering the provided template byte array and replacing placeholders with values from the given dictionary.
    /// </summary>
    /// <param name="template">The HTML template byte array.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteHtmlResponse(
         byte[] template,
         IReadOnlyDictionary<string, object?>? vars,
         int statusCode = 200) => WriteHtmlResponseAsync(Encoding.GetString(template), vars, statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously reads an HTML file, merges in placeholders from the provided dictionary, and writes the result as a response.
    /// </summary>
    /// <param name="filePath">The path to the HTML file to read.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public async Task WriteHtmlResponseFromFileAsync(
        string filePath,
        IReadOnlyDictionary<string, object?> vars,
        int statusCode = 200)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Writing HTML response from file (async), FilePath={FilePath}, StatusCode={StatusCode}", filePath, statusCode);
        }

        if (!File.Exists(filePath))
        {
            WriteTextResponse($"<!-- File not found: {filePath} -->", 404, "text/html");
            return;
        }

        var template = await File.ReadAllTextAsync(filePath);
        WriteHtmlResponseAsync(template, vars, statusCode).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Renders the given HTML string with placeholders and writes it as a response.
    /// </summary>
    /// <param name="template">The HTML template string containing placeholders.</param>
    /// <param name="vars">A dictionary of variables to replace in the template.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteHtmlResponse(
        string template,
        IReadOnlyDictionary<string, object?>? vars,
        int statusCode = 200) => WriteHtmlResponseAsync(template, vars, statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Reads an .html file, merges in placeholders, and writes it.
    /// </summary>
    public void WriteHtmlResponseFromFile(
        string filePath,
        IReadOnlyDictionary<string, object?> vars,
        int statusCode = 200) => WriteHtmlResponseFromFileAsync(filePath, vars, statusCode).GetAwaiter().GetResult();

    /// <summary>
    /// Writes only the specified HTTP status code, clearing any body or content type.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to write.</param>
    public void WriteStatusOnly(int statusCode)
    {
        // Clear any body indicators so StatusCodePages can run
        ContentType = null;
        StatusCode = statusCode;
        Body = null;
    }
    #endregion

    #region Apply to HttpResponse

    /// <summary>
    /// Applies the current KestrunResponse to the specified HttpResponse, setting status, headers, cookies, and writing the body.
    /// </summary>
    /// <param name="response">The HttpResponse to apply the response to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ApplyTo(HttpResponse response)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Applying KestrunResponse to HttpResponse, StatusCode={StatusCode}, ContentType={ContentType}, BodyType={BodyType}",
             StatusCode, ContentType, Body?.GetType().Name ?? "null");
        }

        if (response.StatusCode == StatusCodes.Status304NotModified)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Response already has status code 304 Not Modified, skipping ApplyTo");
            }
            return;
        }
        if (!string.IsNullOrEmpty(RedirectUrl))
        {
            response.Redirect(RedirectUrl);
            return;
        }

        try
        {
            EnsureStatus(response);
            // Apply headers, cookies, caching
            ApplyHeadersAndCookies(response);
            // Caching
            ApplyCachingHeaders(response);
            // Callbacks
            await TryEnqueueCallbacks();
            // Body
            await WriteResponseContent(response);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error applying KestrunResponse to HttpResponse");
            // Optionally, you can log the exception or handle it as needed
            throw;
        }
    }

    /// <summary>
    /// Applies the body content to the HTTP response.  If the body is not null, it ensures the content type,
    /// applies the content disposition header, and writes the body asynchronously.  If the body is null,
    /// it clears the content type and content length if the response has not started.  Logs debug information about the response state.
    /// </summary>
    /// <param name="response"> The HTTP response to which the body content will be applied. </param>
    /// <returns> A task representing the asynchronous operation. </returns>
    private async Task WriteResponseContent(HttpResponse response)
    {
        if (Body is not null)
        {
            EnsureContentType(response);
            ApplyContentDispositionHeader(response);
            await WriteBodyAsync(response).ConfigureAwait(false);
        }
        else
        {
            if (!response.HasStarted && string.IsNullOrEmpty(response.ContentType))
            {
                response.ContentType = null;
            }

            if (!response.HasStarted)
            {
                response.ContentLength = null;
            }
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Status-only: HasStarted={HasStarted} CL={CL} CT='{CT}'",
                 response.HasStarted, response.ContentLength, response.ContentType);
            }
        }
    }

    /// <summary>
    /// Attempts to enqueue any registered callback requests using the ICallbackDispatcher service.
    /// </summary>
    private async ValueTask TryEnqueueCallbacks()
    {
        if (CallbackPlan.Count == 0)
        {
            return;
        }

        // Prevent multiple enqueues
        if (Interlocked.Exchange(ref CallbacksEnqueuedFlag, 1) == 1)
        {
            return;
        }

        if (Logger.IsEnabled(LogEventLevel.Information))
        {
            Logger.Information("Enqueuing {Count} callbacks for dispatch.", CallbackPlan.Count);
        }

        try
        {
            var httpCtx = KrContext.HttpContext;

            // Resolve DI services while request is alive
            var dispatcher = httpCtx.RequestServices.GetService<ICallbackDispatcher>();
            if (dispatcher is null)
            {
                Logger.Warning("Callbacks present but no ICallbackDispatcher registered. Count={Count}", CallbackPlan.Count);
                return;
            }

            var urlResolver = httpCtx.RequestServices.GetRequiredService<ICallbackUrlResolver>();
            var serializer = httpCtx.RequestServices.GetRequiredService<ICallbackBodySerializer>();
            var options = httpCtx.RequestServices.GetService<CallbackDispatchOptions>() ?? new CallbackDispatchOptions();

            foreach (var plan in CallbackPlan)
            {
                try
                {
                    var req = CallbackRequestFactory.FromPlan(plan, KrContext, urlResolver, serializer, options);

                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("Enqueue callback. CallbackId={CallbackId} Url={Url}", req.CallbackId, req.TargetUrl);
                    }

                    await dispatcher.EnqueueAsync(req, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to enqueue callback. CallbackId={CallbackId}", plan.CallbackId);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error while scheduling callbacks.");
        }
    }

    /// <summary>
    /// Ensures the HTTP response has the correct status code and content type.
    /// </summary>
    /// <param name="response">The HTTP response to apply the status and content type to.</param>
    private void EnsureContentType(HttpResponse response)
    {
        if (ContentType != response.ContentType)
        {
            if (!string.IsNullOrEmpty(ContentType) &&
                IsTextBasedContentType(ContentType) &&
                !ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
            {
                ContentType = ContentType.TrimEnd(';') + $"; charset={AcceptCharset.WebName}";
            }
            response.ContentType = ContentType;
        }
    }

    /// <summary>
    /// Ensures the HTTP response has the correct status code.
    /// </summary>
    /// <param name="response">The HTTP response to apply the status code to.</param>
    private void EnsureStatus(HttpResponse response)
    {
        if (StatusCode != response.StatusCode)
        {
            response.StatusCode = StatusCode;
        }
    }

    /// <summary>
    /// Adds caching headers to the response based on the provided CacheControlHeaderValue options.
    /// </summary>
    /// <param name="response">The HTTP response to apply caching headers to.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public void ApplyCachingHeaders(HttpResponse response)
    {
        if (CacheControl is not null)
        {
            response.Headers.CacheControl = CacheControl.ToString();
        }
    }

    /// <summary>
    /// Applies the Content-Disposition header to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to apply the header to.</param>
    private void ApplyContentDispositionHeader(HttpResponse response)
    {
        if (ContentDisposition.Type == ContentDispositionType.NoContentDisposition)
        {
            return;
        }

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Setting Content-Disposition header, Type={Type}, FileName={FileName}",
                      ContentDisposition.Type, ContentDisposition.FileName);
        }

        var dispositionValue = ContentDisposition.Type switch
        {
            ContentDispositionType.Attachment => "attachment",
            ContentDispositionType.Inline => "inline",
            _ => throw new InvalidOperationException("Invalid Content-Disposition type")
        };

        if (string.IsNullOrEmpty(ContentDisposition.FileName) && Body is IFileInfo fi)
        {
            // default filename: use the file's name
            ContentDisposition.FileName = fi.Name;
        }

        if (!string.IsNullOrEmpty(ContentDisposition.FileName))
        {
            var escapedFileName = WebUtility.UrlEncode(ContentDisposition.FileName);
            dispositionValue += $"; filename=\"{escapedFileName}\"";
        }

        response.Headers.Append("Content-Disposition", dispositionValue);
    }

    /// <summary>
    /// Applies headers and cookies to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to apply the headers and cookies to.</param>
    private void ApplyHeadersAndCookies(HttpResponse response)
    {
        if (Headers is not null)
        {
            foreach (var kv in Headers)
            {
                response.Headers[kv.Key] = kv.Value;
            }
        }
        if (Cookies is not null)
        {
            foreach (var cookie in Cookies)
            {
                response.Headers.Append("Set-Cookie", cookie);
            }
        }
    }

    /// <summary>
    /// Writes the response body to the HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response to write to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task WriteBodyAsync(HttpResponse response)
    {
        var bodyValue = Body; // capture to avoid nullability warnings when mutated in default
        switch (bodyValue)
        {
            case IFileInfo fileInfo:
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("Sending file {FileName} (Length={Length})", fileInfo.Name, fileInfo.Length);
                }
                response.ContentLength = fileInfo.Length;
                response.Headers.LastModified = fileInfo.LastModified.ToString("R");
                await response.SendFileAsync(
                    file: fileInfo,
                    offset: 0,
                    count: fileInfo.Length,
                    cancellationToken: response.HttpContext.RequestAborted
                );
                break;

            case byte[] bytes:
                response.ContentLength = bytes.LongLength;
                await response.Body.WriteAsync(bytes, response.HttpContext.RequestAborted);
                await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                break;

            case Stream stream:
                var seekable = stream.CanSeek;
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("Sending stream (seekable={Seekable}, len={Len})",
                          seekable, seekable ? stream.Length : -1);
                }
                if (seekable)
                {
                    response.ContentLength = stream.Length;
                    stream.Position = 0;
                }
                else
                {
                    response.ContentLength = null;
                }

                const int BufferSize = 64 * 1024; // 64 KB
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), response.HttpContext.RequestAborted)) > 0)
                    {
                        await response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), response.HttpContext.RequestAborted);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                break;

            case string str:
                var data = AcceptCharset.GetBytes(str);
                response.ContentLength = data.Length;
                await response.Body.WriteAsync(data, response.HttpContext.RequestAborted);
                await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                break;

            default:
                var bodyType = bodyValue?.GetType().Name ?? "null";
                Body = "Unsupported body type: " + bodyType;
                Logger.Warning("Unsupported body type: {BodyType}", bodyType);
                response.StatusCode = StatusCodes.Status500InternalServerError;
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentLength = Body.ToString()?.Length ?? null;
                break;
        }
    }
    #endregion
}
