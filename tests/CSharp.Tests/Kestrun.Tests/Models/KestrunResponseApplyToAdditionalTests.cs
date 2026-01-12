using Kestrun.Callback;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Xunit;

namespace KestrunTests.Models;

public partial class KestrunResponseTests
{
    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_WhenRedirectUrlSet_UsesRedirectAndSkipsBodyWrite()
    {
        var ctx = TestRequestFactory.CreateContext();
        var res = ctx.Response;
        res.WriteRedirectResponse("https://example.invalid/target");

        var http = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http.Response.Body = ms;

        await res.ApplyTo(http.Response);

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.True(http.Response.Headers.TryGetValue("Location", out var location));
        Assert.Equal("https://example.invalid/target", location.ToString());

        Assert.Equal(0, ms.Length);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_AppendsSetCookieHeaders_WhenCookiesPresent()
    {
        var ctx = TestRequestFactory.CreateContext();
        var res = ctx.Response;

        res.Cookies =
        [
            "a=b; Path=/; HttpOnly",
            "c=d; Path=/"
        ];

        await res.WriteTextResponseAsync("ok", StatusCodes.Status200OK, "text/plain");

        var http = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http.Response.Body = ms;

        await res.ApplyTo(http.Response);

        Assert.True(http.Response.Headers.TryGetValue("Set-Cookie", out var cookies));
        Assert.True(cookies.Count > 0);
        var all = cookies.ToArray();
        Assert.Contains(all, c => c is not null && c.Contains("a=b", StringComparison.Ordinal));
        Assert.Contains(all, c => c is not null && c.Contains("c=d", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_SetsCacheControlHeader_WhenConfigured()
    {
        var ctx = TestRequestFactory.CreateContext();
        var res = ctx.Response;

        res.CacheControl = new CacheControlHeaderValue
        {
            NoStore = true,
            NoCache = true
        };

        await res.WriteTextResponseAsync("ok", StatusCodes.Status200OK, "text/plain");

        var http = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http.Response.Body = ms;

        await res.ApplyTo(http.Response);

        Assert.True(http.Response.Headers.TryGetValue(HeaderNames.CacheControl, out var cc));
        Assert.Contains("no-store", cc.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_StatusOnly_ClearsContentTypeAndLength()
    {
        var ctx = TestRequestFactory.CreateContext();
        var res = ctx.Response;

        res.WriteStatusOnly(StatusCodes.Status204NoContent);

        var http = new DefaultHttpContext();
        http.Response.ContentType = "text/plain";
        http.Response.ContentLength = 123;

        await res.ApplyTo(http.Response);

        Assert.Equal(StatusCodes.Status204NoContent, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
        Assert.Null(http.Response.ContentLength);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task ApplyTo_WithCallbacksButNoDispatcher_DoesNotThrow_AndStillWritesBody()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.RequestServices = new ServiceCollection().BuildServiceProvider();
        });

        var res = ctx.Response;

        var callbackPlan = new CallbackPlan(
            CallbackId: "cb",
            UrlTemplate: "https://example.invalid/callback",
            Method: HttpMethod.Post,
            OperationId: "op",
            PathParams: [],
            Body: null);

        res.CallbackPlan.Add(new CallBackExecutionPlan(
            CallbackId: "cb",
            Plan: callbackPlan,
            BodyParameterName: null,
            Parameters: []));

        await res.WriteTextResponseAsync("hello", StatusCodes.Status200OK, "text/plain");

        var http2 = new DefaultHttpContext();
        using var ms = new MemoryStream();
        http2.Response.Body = ms;

        await res.ApplyTo(http2.Response);

        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8);
        Assert.Equal("hello", reader.ReadToEnd());
    }
}
