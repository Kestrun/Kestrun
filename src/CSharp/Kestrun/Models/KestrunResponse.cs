
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using Serilog;
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

namespace Kestrun.Models;

/// <summary>
/// Represents an HTTP response in the Kestrun framework, providing methods to write various content types and manage headers, cookies, and status codes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunResponse"/> class with the specified request and optional body async threshold.
/// </remarks>
/// <param name="request">The associated <see cref="KestrunRequest"/> for this response.</param>
/// <param name="bodyAsyncThreshold">The threshold in bytes for using async body write operations. Defaults to 8192.</param>
public class KestrunResponse(KestrunRequest request, int bodyAsyncThreshold = 8192)
{
    /// <summary>
    /// A set of MIME types that are considered text-based for response content.
    /// </summary>
    public static readonly HashSet<string> TextBasedMimeTypes =
    new(StringComparer.OrdinalIgnoreCase)
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
    /// Gets the <see cref="HttpContext"/> associated with the response.
    /// </summary>
    public HttpContext Context => Request.Context;
    /// <summary>
    /// Gets or sets the HTTP status code for the response.
    /// </summary>
    public int StatusCode { get; set; } = request.Context.Response.StatusCode;
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
    public KestrunRequest Request { get; private set; } = request ?? throw new ArgumentNullException(nameof(request));

    /// <summary>
    /// Global text encoding for all responses. Defaults to UTF-8.
    /// </summary>
    public Encoding AcceptCharset { get; private set; } = request.Headers.TryGetValue("Accept-Charset", out var value) ? Encoding.GetEncoding(value) : Encoding.UTF8; // Default to UTF-8 if null

    /// <summary>
    /// If the response body is larger than this threshold (in bytes), async write will be used.
    /// </summary>
    public int BodyAsyncThreshold { get; set; } = bodyAsyncThreshold;

    /// <summary>
    /// Cache-Control header value for the response.
    /// </summary>
    public CacheControlHeaderValue? CacheControl { get; set; }

    #region Constructors
    #endregion

    #region Helpers
    /// <summary>
    /// Retrieves the value of the specified header from the response headers.
    /// </summary>
    /// <param name="key">The name of the header to retrieve.</param>
    /// <returns>The value of the header if found; otherwise, null.</returns>
    public string? GetHeader(string key) => Headers.TryGetValue(key, out var value) ? value : null;

    private string DetermineContentType(string? contentType, string defaultType = "text/plain")
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            _ = Request.Headers.TryGetValue("Accept", out var acceptHeader);
            contentType = (acceptHeader ?? defaultType)
                                 .ToLowerInvariant();
        }

        return contentType;
    }

    /// <summary>
    /// Determines whether the specified content type is text-based or supports a charset.
    /// </summary>
    /// <param name="type">The MIME content type to check.</param>
    /// <returns>True if the content type is text-based; otherwise, false.</returns>
    public static bool IsTextBasedContentType(string type)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Checking if content type is text-based: {ContentType}", type);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing file response,FilePath={FilePath} StatusCode={StatusCode}, ContentType={ContentType}, CurrentDirectory={CurrentDirectory}",
                filePath, statusCode, contentType, Directory.GetCurrentDirectory());
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            StatusCode = StatusCodes.Status404NotFound;
            Body = $"File not found: {filePath}";
            ContentType = $"text/plain; charset={Encoding.WebName}";
            return;
        }
        // 1. Make sure you have an absolute file path
        var fullPath = Path.GetFullPath(filePath);

        // 2. Extract the directory to use as the "root"
        var directory = Path.GetDirectoryName(fullPath)
                       ?? throw new InvalidOperationException("Could not determine directory from file path");

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Serving file: {FilePath}", fullPath);
        }

        // Create a physical file provider for the directory
        var physicalProvider = new PhysicalFileProvider(directory);
        var fi = physicalProvider.GetFileInfo(Path.GetFileName(filePath));
        var provider = new FileExtensionContentTypeProvider();
        contentType ??= provider.TryGetContentType(fullPath, out var ct)
                ? ct
                : "application/octet-stream";
        Body = fi;

        // headers & metadata
        StatusCode = statusCode;
        ContentType = contentType;
        Log.Debug("File response prepared: FileName={FileName}, Length={Length}, ContentType={ContentType}",
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
    /// <param name="serializerSettings">The settings to use for JSON serialization.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public void WriteJsonResponse(object? inputObject, JsonSerializerSettings serializerSettings, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteJsonResponseAsync(inputObject, serializerSettings, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a JSON response using the specified input object and serializer settings.
    /// </summary>
    /// <param name="inputObject">The object to be converted to JSON.</param>
    /// <param name="serializerSettings">The settings to use for JSON serialization.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteJsonResponseAsync(object? inputObject, JsonSerializerSettings serializerSettings, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing JSON response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        Body = await Task.Run(() => JsonConvert.SerializeObject(inputObject, serializerSettings));
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing JSON response (async), StatusCode={StatusCode}, ContentType={ContentType}, Depth={Depth}, Compress={Compress}",
                statusCode, contentType, depth, compress);
        }

        var serializerSettings = new JsonSerializerSettings
        {
            Formatting = compress ? Formatting.None : Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MaxDepth = depth
        };
        await WriteJsonResponseAsync(inputObject, serializerSettings: serializerSettings, statusCode: statusCode, contentType: contentType);
    }
    /// <summary>
    /// Writes a CBOR response (binary, efficient, not human-readable).
    /// </summary>
    public async Task WriteCborResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing CBOR response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing BSON response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
    /// Asynchronously writes a response with the specified input object and HTTP status code.
    /// Chooses the response format based on the Accept header or defaults to text/plain.
    /// </summary>
    /// <param name="inputObject">The object to be sent in the response body.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public async Task WriteResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing response, StatusCode={StatusCode}", statusCode);
        }

        Body = inputObject;
        ContentType = DetermineContentType(contentType: string.Empty); // Ensure ContentType is set based on Accept header
        if (ContentType.Contains(','))
        {
            var ContentTypes = ContentType.Split(','); // Take the first type only
            ContentType = "application/json"; // fallback
            foreach (var ct in ContentTypes)
            {
                if (ct.Contains("json") || ct.Contains("xml") || ct.Contains("yaml") || ct.Contains("yml"))
                {
                    if (Log.IsEnabled(LogEventLevel.Verbose))
                    {
                        Log.Verbose("Multiple content types in Accept header, selecting {ContentType}", ct);
                    }
                    ContentType = ct;
                    break;
                }
            }
        }
        if (Log.IsEnabled(LogEventLevel.Verbose))
        {
            Log.Verbose("Determined ContentType={ContentType}", ContentType);
        }
        if (ContentType.Contains("json"))
        {
            await WriteJsonResponseAsync(inputObject: inputObject, statusCode: statusCode, contentType: ContentType);
        }
        else if (ContentType.Contains("yaml") || ContentType.Contains("yml"))
        {
            await WriteYamlResponseAsync(inputObject: inputObject, statusCode: statusCode, contentType: ContentType);
        }
        else if (ContentType.Contains("xml"))
        {
            await WriteXmlResponseAsync(inputObject: inputObject, statusCode: statusCode, contentType: ContentType);
        }
        else
        {
            await WriteTextResponseAsync(inputObject: inputObject?.ToString() ?? string.Empty, statusCode: statusCode);
        }
    }

    /// <summary>
    /// Writes a response with the specified input object and HTTP status code.
    /// Chooses the response format based on the Accept header or defaults to text/plain.
    /// </summary>
    /// <param name="inputObject">The object to be sent in the response body.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    public void WriteResponse(object? inputObject, int statusCode = StatusCodes.Status200OK) => WriteResponseAsync(inputObject, statusCode).GetAwaiter().GetResult();

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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing CSV response (async), StatusCode={StatusCode}, ContentType={ContentType}",
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing YAML response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing XML response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        }

        var root = string.IsNullOrWhiteSpace(rootElementName) ? "Response" : rootElementName!.Trim();
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
    public void WriteTextResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null) => WriteTextResponseAsync(inputObject, statusCode, contentType).GetAwaiter().GetResult();

    /// <summary>
    /// Asynchronously writes a text response with the specified input object, status code, and content type.
    /// </summary>
    /// <param name="inputObject">The object to be converted to a text response.</param>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public async Task WriteTextResponseAsync(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing text response (async), StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
    /// Writes an HTTP redirect response with the specified URL and optional message.
    /// </summary>
    /// <param name="url">The URL to redirect to.</param>
    /// <param name="message">An optional message to include in the response body.</param>
    public void WriteRedirectResponse(string url, string? message = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing redirect response, StatusCode={StatusCode}, Location={Location}", StatusCode, url);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing binary response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing stream response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing error response, StatusCode={StatusCode}, ContentType={ContentType}, Message={Message}",
                statusCode, contentType, message);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentNullException(nameof(message));
        }

        Log.Warning("Writing error response with status {StatusCode}: {Message}", statusCode, message);

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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing error response from exception, StatusCode={StatusCode}, ContentType={ContentType}, IncludeStack={IncludeStack}",
                statusCode, contentType, includeStack);
        }

        ArgumentNullException.ThrowIfNull(ex);

        Log.Warning(ex, "Writing error response with status {StatusCode}", statusCode);

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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing formatted error response, ContentType={ContentType}, Status={Status}", contentType, payload.Status);
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
    private static string RenderInlineTemplate(
     string template,
     IReadOnlyDictionary<string, object?> vars)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Rendering inline template, TemplateLength={TemplateLength}, VarsCount={VarsCount}",
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

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Rendered template length: {RenderedLength}", render.Length);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing HTML response (async), StatusCode={StatusCode}, TemplateLength={TemplateLength}", statusCode, template.Length);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Writing HTML response from file (async), FilePath={FilePath}, StatusCode={StatusCode}", filePath, statusCode);
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Applying KestrunResponse to HttpResponse, StatusCode={StatusCode}, ContentType={ContentType}, BodyType={BodyType}",
                StatusCode, ContentType, Body?.GetType().Name ?? "null");
        }

        if (response.StatusCode == StatusCodes.Status304NotModified)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Response already has status code 304 Not Modified, skipping ApplyTo");
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
            ApplyHeadersAndCookies(response);
            ApplyCachingHeaders(response);
            if (Body is not null)
            {
                EnsureContentType(response);
                ApplyContentDispositionHeader(response);
                await WriteBodyAsync(response).ConfigureAwait(false);
            }
            else
            {
                response.ContentType = null;
                response.ContentLength = null;
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Status-only: HasStarted={HasStarted} CL={CL} CT='{CT}'",
                        response.HasStarted, response.ContentLength, response.ContentType);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying response: {ex.Message}");
            // Optionally, you can log the exception or handle it as needed
            throw;
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

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Setting Content-Disposition header, Type={Type}, FileName={FileName}",
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
                Log.Debug("Sending file {FileName} (Length={Length})", fileInfo.Name, fileInfo.Length);
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
                Log.Debug("Sending stream (seekable={Seekable}, len={Len})",
                          seekable, seekable ? stream.Length : -1);

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
                Log.Warning("Unsupported body type: {BodyType}", bodyType);
                response.StatusCode = StatusCodes.Status500InternalServerError;
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentLength = Body.ToString()?.Length ?? null;
                break;
        }
    }
    #endregion
}
