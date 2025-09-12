using Kestrun.Models;
using Microsoft.AspNetCore.Http;

namespace KestrunTests;

/// <summary>
/// Test-only factory for creating KestrunRequest instances via its non-public constructor.
/// </summary>
internal static class TestRequestFactory
{
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
}
