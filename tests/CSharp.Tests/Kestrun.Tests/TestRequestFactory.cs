using Kestrun.Models;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Serilog;

namespace KestrunTests;

/// <summary>
/// Test-only factory for creating KestrunRequest instances via its non-public constructor.
/// </summary>
internal static class TestRequestFactory
{
    private static readonly Lazy<KestrunHost> _host = new(() => new KestrunHost("Tests", Log.Logger));

    private static RouteEndpoint CreateTestEndpoint(string? pattern)
    {
        var p = string.IsNullOrWhiteSpace(pattern) ? "/" : pattern;
        var routePattern = RoutePatternFactory.Parse(p);
        static Task requestDelegate(HttpContext _) => Task.CompletedTask;
        return new RouteEndpoint(requestDelegate, routePattern, order: 0, metadata: EndpointMetadataCollection.Empty, displayName: "TestEndpoint");
    }

    internal static void EnsureRoutedHttpContext(DefaultHttpContext http, string? pattern = null)
    {
        if (string.IsNullOrWhiteSpace(http.Request.Method))
        {
            http.Request.Method = "GET";
        }

        if (!http.Request.Path.HasValue)
        {
            http.Request.Path = "/";
        }

        if (http.GetEndpoint() is not RouteEndpoint)
        {
            http.SetEndpoint(CreateTestEndpoint(pattern ?? http.Request.Path.Value));
        }
    }

    internal static KestrunRequest Create(
        string method = "GET",
        string path = "/",
        Dictionary<string, string>? headers = null,
        string body = "",
        Dictionary<string, string>? form = null,
        Action<DefaultHttpContext>? configureContext = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;

        // Add headers
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                ctx.Request.Headers[kv.Key] = kv.Value;
            }
        }

        // Add form if provided
        if (form != null && form.Count > 0)
        {
            var formCollection = new FormCollection(form.ToDictionary(k => k.Key, v => new Microsoft.Extensions.Primitives.StringValues(v.Value)));
            ctx.Request.ContentType = "application/x-www-form-urlencoded";
            ctx.Request.Form = formCollection;
        }

        // Body
        if (!string.IsNullOrEmpty(body))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
        }

        configureContext?.Invoke(ctx);

        // Use public async factory
        return KestrunRequest.NewRequestSync(ctx);
    }

    internal static KestrunContext CreateContext(
        string method = "GET",
        string path = "/",
        Dictionary<string, string>? headers = null,
        string body = "",
        Dictionary<string, string>? form = null,
        Action<DefaultHttpContext>? configureContext = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;

        EnsureRoutedHttpContext(ctx, pattern: path);

        // Add headers
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                ctx.Request.Headers[kv.Key] = kv.Value;
            }
        }

        // Add form if provided
        if (form != null && form.Count > 0)
        {
            var formCollection = new FormCollection(form.ToDictionary(k => k.Key, v => new Microsoft.Extensions.Primitives.StringValues(v.Value)));
            ctx.Request.ContentType = "application/x-www-form-urlencoded";
            ctx.Request.Form = formCollection;
        }

        // Body
        if (!string.IsNullOrEmpty(body))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
        }

        configureContext?.Invoke(ctx);

        return new KestrunContext(_host.Value, ctx);
    }
}
