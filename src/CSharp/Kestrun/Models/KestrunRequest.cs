using System.Text;

namespace Kestrun.Models;


/// <summary>
/// Represents a request model for Kestrun, containing HTTP method, path, query, headers, body, authorization, cookies, and form data.
/// </summary>
public partial class KestrunRequest
{
    private KestrunRequest(HttpContext context, Dictionary<string, string>? formDict, string body)
    {
        HttpContext = context ?? throw new ArgumentNullException(nameof(context));
        Request = context.Request;
        Query = Request.Query
                            .ToDictionary(x => x.Key, x => x.Value.ToString() ?? string.Empty);
        Headers = Request.Headers
                            .ToDictionary(x => x.Key, x => x.Value.ToString() ?? string.Empty);
        Cookies = Request.Cookies
                            .ToDictionary(x => x.Key, x => x.Value.ToString() ?? string.Empty);
        Form = formDict;
        Body = body;
        RouteValues = Request.RouteValues
                            .ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Gets the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> associated with the request.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets the <see cref="HttpRequest"/> associated with the request.
    /// </summary>
    public HttpRequest Request { get; init; }
    /// <summary>
    /// Gets the HTTP method for the request (e.g., GET, POST).
    /// </summary>
    public string Method => Request.Method;

    /// <summary>
    /// Gets the host header value for the request.
    /// </summary>
    public HostString Host => Request.Host;

    /// <summary>
    /// Gets the query string for the request (e.g., "?id=123").
    /// </summary>
    public string QueryString => Request.QueryString.ToUriComponent();

    /// <summary>
    /// Gets the content type of the request (e.g., "application/json").
    /// </summary>
    public string? ContentType => Request.ContentType;

    /// <summary>
    /// Gets the protocol used for the request (e.g., "HTTP/1.1").
    /// </summary>
    public string Protocol => Request.Protocol;

    /// <summary>
    /// Gets a value indicating whether the request is made over HTTPS.
    /// </summary>
    public bool IsHttps => Request.IsHttps;

    /// <summary>
    /// Gets the content length of the request, if available.
    /// </summary>
    public long? ContentLength => Request.ContentLength;

    /// <summary>
    /// Gets a value indicating whether the request has a form content type.
    /// </summary>
    public bool HasFormContentType => Request.HasFormContentType;

    /// <summary>
    /// Gets the request scheme (e.g., "http", "https").
    /// </summary>
    public string Scheme => Request.Scheme;

    /// <summary>
    /// Gets the request path (e.g., "/api/resource").
    /// </summary>
    public string Path => Request.Path.ToString();

    /// <summary>
    /// Gets the base path for the request (e.g., "/api").
    /// </summary>
    public string PathBase => Request.PathBase.ToString();

    /// <summary>
    /// Gets the query parameters for the request as a dictionary of key-value pairs.
    /// </summary>
    public Dictionary<string, string> Query { get; init; }


    /// <summary>
    /// Gets the headers for the request as a dictionary of key-value pairs.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Gets the body content of the request as a string.
    /// </summary>
    public string Body { get; init; }

    /// <summary>
    /// Gets the authorization header value for the request, if present.
    /// </summary>
    public string? Authorization => Request.Headers.Authorization.ToString();
    /// <summary>
    /// Gets the cookies for the request as an <see cref="IRequestCookieCollection"/>, if present.
    /// </summary>
    public Dictionary<string, string> Cookies { get; init; }

    /// <summary>
    /// Gets the form data for the request as a dictionary of key-value pairs, if present.
    /// </summary>
    public Dictionary<string, string>? Form { get; init; }


    /// <summary>
    /// Gets the route values for the request as a <see cref="RouteValueDictionary"/>, if present.
    /// </summary>
    public Dictionary<string, string> RouteValues { get; init; }


    /// <summary>
    /// Creates a new <see cref="KestrunRequest"/> instance from the specified <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </summary>
    /// <param name="context">The HTTP context containing the request information.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the constructed <see cref="KestrunRequest"/>.</returns>
    public static async Task<KestrunRequest> NewRequest(HttpContext context)
    {
        // ① Allow the body to be read multiple times
        context.Request.EnableBuffering();

        // ② Read the raw body into a string, then rewind
        string body;
        using (var reader = new StreamReader(
                   context.Request.Body,
                   encoding: Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // ③ If it's a form, read it safely
        var formDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            foreach (var kv in form)
            {
                formDict[kv.Key] = kv.Value.ToString();
            }
        }

        return new KestrunRequest(context: context, formDict: formDict, body: body);
    }

    /// <summary>
    /// Synchronous helper for tests and simple call sites that prefer not to use async/await.
    /// Avoid in ASP.NET request pipelines; intended for unit tests only.
    /// </summary>
    public static KestrunRequest NewRequestSync(HttpContext context) => NewRequest(context).GetAwaiter().GetResult();
}
