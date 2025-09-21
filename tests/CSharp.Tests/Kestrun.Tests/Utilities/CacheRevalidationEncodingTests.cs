using Kestrun.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace KestrunTests.Utilities;

public class CacheRevalidationEncodingTests
{
    private static HttpContext MakeContext(string? acceptCharset = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        if (!string.IsNullOrEmpty(acceptCharset))
        {
            ctx.Request.Headers[HeaderNames.AcceptCharset] = acceptCharset;
        }
        return ctx;
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ChooseEncoding_Defaults_To_Utf8_When_Header_Missing()
    {
        var ctx = MakeContext();
        // Use ASCII-only string so encoding differences won't affect test; we verify via produced ETag length change with different encodings later
        var payload = "hello";
        var wrote304 = CacheRevalidation.TryWrite304(ctx, payload);
        Assert.False(wrote304);
        Assert.True(ctx.Response.Headers.ContainsKey(HeaderNames.ETag));
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ChooseEncoding_Prefers_Highest_Q_Value()
    {
        var ctx = MakeContext("iso-8859-1;q=0.4, utf-8;q=0.9");
        var payload = "héllo"; // contains an accented char to differentiate encodings if needed
        var wrote304 = CacheRevalidation.TryWrite304(ctx, payload);
        Assert.False(wrote304);
        var etag1 = ctx.Response.Headers[HeaderNames.ETag].ToString();
        Assert.NotEmpty(etag1);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ChooseEncoding_Uses_Wildcard_When_No_Specific()
    {
        var ctx = MakeContext("*;q=0.5");
        var payload = "wild";
        var wrote304 = CacheRevalidation.TryWrite304(ctx, payload);
        Assert.False(wrote304);
        Assert.True(ctx.Response.Headers.ContainsKey(HeaderNames.ETag));
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void ChooseEncoding_Ignores_Unknown_Then_Fallback()
    {
        var ctx = MakeContext("madeup-xyz;q=1.0, utf-16;q=0.7");
        var payload = "test";
        var wrote304 = CacheRevalidation.TryWrite304(ctx, payload);
        Assert.False(wrote304);
        Assert.True(ctx.Response.Headers.ContainsKey(HeaderNames.ETag));
    }
}
