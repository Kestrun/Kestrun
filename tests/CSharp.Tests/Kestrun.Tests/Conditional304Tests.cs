using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;
using Kestrun.Utilities;

namespace KestrunTests;

public class Conditional304Tests
{
    private static DefaultHttpContext NewCtx()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public void TryWrite304_EtagMatch_Returns304()
    {
        var ctx = NewCtx();
        var payload = "hello world"; // will derive etag
                                     // First call to compute validators
        var miss = CacheRevalidation.TryWrite304(ctx, payload, weakETag: false);
        Assert.False(miss); // not 304
        var etag = ctx.Response.Headers[HeaderNames.ETag].ToString();
        Assert.False(string.IsNullOrEmpty(etag));

        // Simulate subsequent request with If-None-Match
        var ctx2 = NewCtx();
        ctx2.Request.Method = HttpMethods.Get;
        ctx2.Request.Headers[HeaderNames.IfNoneMatch] = etag;
        var is304 = CacheRevalidation.TryWrite304(ctx2, payload, weakETag: false);
        Assert.True(is304);
        Assert.Equal(StatusCodes.Status304NotModified, ctx2.Response.StatusCode);
    }

    [Fact]
    public void TryWrite304_LastModifiedMatch_Returns304()
    {
        var lm = DateTimeOffset.UtcNow.AddMinutes(-10);
        var ctx = NewCtx();
        var payload = Encoding.UTF8.GetBytes("abc");
        // first pass no If-Modified-Since header
        var miss = CacheRevalidation.TryWrite304(ctx, payload, lastModified: lm);
        Assert.False(miss);

        var ctx2 = NewCtx();
        ctx2.Request.Method = HttpMethods.Get;
        ctx2.Request.Headers[HeaderNames.IfModifiedSince] = lm.UtcDateTime.ToString("R");
        var is304 = CacheRevalidation.TryWrite304(ctx2, payload, lastModified: lm);
        Assert.True(is304);
        Assert.Equal(StatusCodes.Status304NotModified, ctx2.Response.StatusCode);
    }

    [Fact]
    public void TryWrite304_WeakEtag_AppliesPrefix()
    {
        var ctx = NewCtx();
        var payload = "weak";
        var _ = CacheRevalidation.TryWrite304(ctx, payload, weakETag: true);
        var etag = ctx.Response.Headers[HeaderNames.ETag].ToString();
        Assert.StartsWith("W/\"", etag);
    }

    [Fact]
    public void TryWrite304_UnsafeMethod_DoesNotReturn304()
    {
        var ctx = NewCtx();
        ctx.Request.Method = HttpMethods.Post;
        var payload = "post body";
        var result = CacheRevalidation.TryWrite304(ctx, payload);
        Assert.False(result);
        Assert.NotEqual(StatusCodes.Status304NotModified, ctx.Response.StatusCode);
    }

    [Fact]
    public void TryWrite304_DifferentPayloadTypes_AllHash()
    {
        var ctx1 = NewCtx();
        var _ = CacheRevalidation.TryWrite304(ctx1, "string payload");
        Assert.NotEmpty(ctx1.Response.Headers[HeaderNames.ETag].ToString());

        var ctx2 = NewCtx();
        _ = CacheRevalidation.TryWrite304(ctx2, Encoding.UTF8.GetBytes("bytes"));
        Assert.NotEmpty(ctx2.Response.Headers[HeaderNames.ETag].ToString());

        var ctx3 = NewCtx();
        _ = CacheRevalidation.TryWrite304(ctx3, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("rom")));
        Assert.NotEmpty(ctx3.Response.Headers[HeaderNames.ETag].ToString());

        var ctx4 = NewCtx();
        _ = CacheRevalidation.TryWrite304(ctx4, new Memory<byte>(Encoding.UTF8.GetBytes("mem")));
        Assert.NotEmpty(ctx4.Response.Headers[HeaderNames.ETag].ToString());

        var backing = Encoding.UTF8.GetBytes("array-seg");
        var ctx5 = NewCtx();
        _ = CacheRevalidation.TryWrite304(ctx5, new ArraySegment<byte>(backing, 0, backing.Length));
        Assert.NotEmpty(ctx5.Response.Headers[HeaderNames.ETag].ToString());

        var ctx6 = NewCtx();
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("stream")))
        {
            _ = CacheRevalidation.TryWrite304(ctx6, ms);
        }
        Assert.NotEmpty(ctx6.Response.Headers[HeaderNames.ETag].ToString());
    }
}
