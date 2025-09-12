using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using Xunit;

namespace KestrunTests.Models;

public class KestrunRequestTests
{
    private static DefaultHttpContext MakeContext(
        string method = "POST",
        string path = "/api/test",
        string? body = "data")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Headers["Authorization"] = "Bearer token";
        ctx.Request.QueryString = new QueryString("?a=1&b=two");
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
        }
        return ctx;
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task NewRequest_Reads_Body_And_Headers_And_Query()
    {
        var ctx = MakeContext();
        var req = await KestrunRequest.NewRequest(ctx);

        Assert.Equal("POST", req.Method);
        Assert.Equal("/api/test", req.Path);
        Assert.Equal("1", req.Query["a"]);
        Assert.Equal("two", req.Query["b"]);
        Assert.Equal("Bearer token", req.Authorization);
        Assert.Equal("data", req.Body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task NewRequest_Reads_Form_When_HasFormContentType()
    {
        var ctx = MakeContext(body: null);
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var formBody = "foo=bar&baz=1&baz=2";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(formBody));
        ctx.Request.ContentLength = Encoding.UTF8.GetByteCount(formBody);

        var req = await KestrunRequest.NewRequest(ctx);

        Assert.NotNull(req.Form);
        Assert.Equal("bar", req.Form!["foo"]);
        Assert.Equal("1,2", req.Form!["baz"]);
        Assert.True(req.HasFormContentType);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task NewRequest_Populates_All_Metadata_And_Collections()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Scheme = "https";
        ctx.Request.IsHttps = true;
        ctx.Request.Host = new HostString("example.com", 8443);
        ctx.Request.PathBase = "/base";
        ctx.Request.Path = "/resource";
        ctx.Request.QueryString = new QueryString("?one=1&two=second");
        ctx.Request.Headers["X-Custom"] = "value";
        ctx.Request.Headers["Authorization"] = "Bearer abc";
        ctx.Request.Headers["Cookie"] = "session=abc123; theme=dark";
        ctx.Request.RouteValues["id"] = "42";
        ctx.Request.Protocol = "HTTP/2"; // set explicitly to ensure coverage
        var bodyText = "hello world";
        var bodyBytes = Encoding.UTF8.GetBytes(bodyText);
        ctx.Request.Body = new MemoryStream(bodyBytes);
        ctx.Request.ContentLength = bodyBytes.Length;

        var req = await KestrunRequest.NewRequest(ctx);

        Assert.Equal("GET", req.Method);
        Assert.Equal("https", req.Scheme);
        Assert.True(req.IsHttps);
        Assert.Equal("example.com:8443", req.Host);
        Assert.Equal("/base", req.PathBase);
        Assert.Equal("/resource", req.Path);
        Assert.Equal("?one=1&two=second", req.QueryString);
        Assert.Equal("1", req.Query["one"]);
        Assert.Equal("second", req.Query["two"]);
        Assert.Equal("value", req.Headers["X-Custom"]);
        Assert.Equal("hello world", req.Body);
        Assert.Equal(bodyBytes.Length, req.ContentLength);
        Assert.Equal("HTTP/2", req.Protocol);
        Assert.Equal("42", req.RouteValues["id"]);
        Assert.Equal("Bearer abc", req.Authorization);
        Assert.Equal("abc123", req.Cookies["session"]);
        Assert.Equal("dark", req.Cookies["theme"]);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task NewRequest_CanBeCalledTwice_With_Body_ReReadable()
    {
        var ctx = MakeContext(method: "POST", path: "/twice", body: "repeat");
        var first = await KestrunRequest.NewRequest(ctx);
        var second = await KestrunRequest.NewRequest(ctx); // should succeed due to EnableBuffering + rewind
        Assert.Equal("repeat", first.Body);
        Assert.Equal(first.Body, second.Body);
    }

    [Fact]
    [Trait("Category", "Models")]
    public async Task NewRequest_NoBody_Returns_Empty_String_And_ContentLength_Null()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/empty";
        var req = await KestrunRequest.NewRequest(ctx);
        Assert.Equal(string.Empty, req.Body);
        Assert.Null(req.ContentLength);
    }
}
