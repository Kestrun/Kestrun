namespace Kestrun.Utilities;

/// <summary>
/// Common HTTP verbs recognized by the framework.
/// </summary>
/// <remarks>
/// This enum includes standard HTTP methods as well as WebDAV extensions.
/// It is designed to be extensible for future HTTP methods. 
/// The enum values correspond to the HTTP methods as defined in various RFCs:
/// RFC 4918 - HTTP Extensions for WebDAV
/// RFC 3744 - WebDAV Access Control Protocol
/// RFC 3253 - Versioning Extensions to WebDAV
/// RFC 5323 - WebDAV SEARCH
/// RFC 5842 - WebDAV Ordered Collections
/// RFC 5689 - WebDAV Bindings
/// RFC 6620 - WebDAV MERGE
/// RFC 5689 - WebDAV BIND
/// RFC 4918 - WebDAV
/// RFC 7231 - HTTP/1.1 Semantics and Content
/// RFC 7232 - HTTP/1.1 Conditional Requests
/// RFC 7233 - HTTP/1.1 Range Requests
/// RFC 7234 - HTTP/1.1 Caching
/// RFC 7235 - HTTP/1.1 Authentication
/// </remarks> 
[Flags]
public enum HttpVerb
{
    /// <summary>
    /// Represents the HTTP GET method.
    /// </summary>
    Get = 1 << 0,
    /// <summary>
    /// Represents the HTTP HEAD method.
    /// </summary>
    Head = 1 << 1,
    /// <summary>
    /// Represents the HTTP POST method.
    /// </summary>
    Post = 1 << 2,
    /// <summary>
    /// Represents the HTTP PUT method.
    /// </summary>
    Put = 1 << 3,
    /// <summary>
    /// Represents the HTTP PATCH method.
    /// </summary>
    Patch = 1 << 4,
    /// <summary>
    /// Represents the HTTP DELETE method.
    /// </summary>
    Delete = 1 << 5,
    /// <summary>
    /// Represents the HTTP OPTIONS method.
    /// </summary>
    Options = 1 << 6,
    /// <summary>
    /// Represents the HTTP TRACE method.
    /// </summary>
    Trace = 1 << 7,
    // WebDAV verbs
    /// <summary>
    /// Represents the HTTP PROPFIND method (WebDAV).
    /// </summary>
    PropFind = 1 << 8,
    /// <summary>
    /// Represents the HTTP PROPPATCH method (WebDAV).
    /// </summary>
    PropPatch = 1 << 9,
    /// <summary>
    /// Represents the HTTP MKCOL method (WebDAV).
    /// </summary>
    MkCol = 1 << 10,
    /// <summary>
    /// Represents the HTTP COPY method (WebDAV).
    /// </summary>
    Copy = 1 << 11,
    /// <summary>
    /// Represents the HTTP MOVE method (WebDAV).
    /// </summary>
    Move = 1 << 12,
    /// <summary>
    /// Represents the HTTP LOCK method (WebDAV).
    /// </summary>
    Lock = 1 << 13,
    /// <summary>
    /// Represents the HTTP UNLOCK method (WebDAV).
    /// </summary>
    Unlock = 1 << 14,
    /// <summary>
    /// Represents the HTTP REPORT method (WebDAV).
    /// </summary>
    Report = 1 << 15,
    /// <summary>
    /// Represents the HTTP ACL method (WebDAV).
    /// </summary>
    Acl = 1 << 16,
    /// <summary>
    /// Represents the HTTP SEARCH method (WebDAV).
    /// </summary>
    Search = 1 << 17,
    /// <summary>
    /// Represents the HTTP MERGE method (WebDAV).
    /// </summary>
    Merge = 1 << 18,
    /// <summary>
    /// Represents the HTTP BIND method (WebDAV).
    /// </summary>
    Bind = 1 << 19,
    /// <summary>
    /// Represents the HTTP UNBIND method (WebDAV).
    /// </summary>
    Unbind = 1 << 20,
    /// <summary>
    /// Represents the HTTP REBIND method (WebDAV).
    /// </summary>
    Rebind = 1 << 21,
    /// <summary>
    /// Represents the HTTP UPDATE method (WebDAV).
    /// </summary>
    Update = 1 << 22,
    /// <summary>
    /// Represents the HTTP VERSION-CONTROL method (WebDAV).
    /// </summary>
    VersionControl = 1 << 23,
    /// <summary>
    /// Represents the HTTP CHECKIN method (WebDAV).
    /// </summary>
    Checkin = 1 << 24,
    /// <summary>
    /// Represents the HTTP CHECKOUT method (WebDAV).
    /// </summary>
    Checkout = 1 << 25,
    /// <summary>
    /// Represents the HTTP UNCHECKOUT method (WebDAV).
    /// </summary>
    Uncheckout = 1 << 26,
    /// <summary>
    /// Represents the HTTP MKWORKSPACE method (WebDAV).
    /// </summary>
    MkWorkspace = 1 << 27,
    /// <summary>
    /// Represents the HTTP LABEL method (WebDAV).
    /// </summary>
    Label = 1 << 28,
    /// <summary>
    /// Represents the HTTP ORDERPATCH method (WebDAV).
    /// </summary>
    OrderPatch = 1 << 29
}

/// <summary>
/// Extension methods for the <see cref="HttpVerb"/> enum.
/// </summary>
public static class HttpVerbExtensions
{
    /// <summary>
    /// Convert the verb enum to its uppercase HTTP method string.
    /// </summary>
    public static string ToMethodString(this HttpVerb v) => v.ToString().ToUpperInvariant();

    /// <summary>
    /// Convert a HTTP method string to its corresponding HttpVerb enum value.
    /// </summary>
    /// <param name="method">The HTTP method string (case-insensitive).</param>
    /// <returns>The corresponding HttpVerb enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the method string is not recognized.</exception>
    public static HttpVerb FromMethodString(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method cannot be null or whitespace.", nameof(method));
        }

        // Handle special cases where enum names don't match HTTP method strings
        var normalizedMethod = method.Trim().ToUpperInvariant() switch
        {
            "PROPFIND" => "PropFind",
            "PROPPATCH" => "PropPatch",
            "MKCOL" => "MkCol",
            "VERSION-CONTROL" => "VersionControl",
            "MKWORKSPACE" => "MkWorkspace",
            "ORDERPATCH" => "OrderPatch",
            _ => method.Trim()
        };

        return Enum.TryParse<HttpVerb>(normalizedMethod, true, out var result)
            ? result
            : throw new ArgumentException($"Unknown HTTP method: {method}", nameof(method));
    }

    /// <summary>
    /// Try to convert a HTTP method string to its corresponding HttpVerb enum value.
    /// </summary>
    /// <param name="method">The HTTP method string (case-insensitive).</param>
    /// <param name="verb">When this method returns, contains the HttpVerb value if conversion succeeded, or default value if it failed.</param>
    /// <returns>true if the conversion succeeded; otherwise, false.</returns>
    public static bool TryFromMethodString(string method, out HttpVerb verb)
    {
        verb = default;

        if (string.IsNullOrWhiteSpace(method))
        {
            return false;
        }

        try
        {
            verb = FromMethodString(method);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
