using Kestrun.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Middleware;

public class FaviconMiddlewareExtensionsTests
{
    [Fact]
    [Trait("Category", "Middleware")]
    public async Task UseFavicon_ServesEmbeddedIcon_OnDefaultPath()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        _ = app.UseFavicon();
        var pipeline = app.Build();

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/favicon.ico";
        ctx.Response.Body = new MemoryStream();

        await pipeline(ctx);

        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Equal("image/x-icon", ctx.Response.ContentType);
        Assert.True(ctx.Response.Headers.ContainsKey("Cache-Control"));
        var cacheHeader = ctx.Response.Headers["Cache-Control"].ToString();
        Assert.StartsWith("public,", cacheHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=31536000", cacheHeader);
        ctx.Response.Body.Position = 0;
        using var ms = new MemoryStream();
        ctx.Response.Body.CopyTo(ms);
        Assert.True(ms.Length > 0);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task UseFavicon_ServesCustomIcon_WhenPathProvided()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            // tiny fake .ico bytes
            var bytes = new byte[] { 0x00, 0x00, 0x01, 0x00 };
            await File.WriteAllBytesAsync(tmp, bytes);

            var services = new ServiceCollection().BuildServiceProvider();
            var app = new ApplicationBuilder(services);
            _ = app.UseFavicon(tmp);
            var pipeline = app.Build();

            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/favicon.ico";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            Assert.Equal(200, ctx.Response.StatusCode);
            Assert.Equal("image/x-icon", ctx.Response.ContentType);
            Assert.True(ctx.Response.Headers.ContainsKey("Cache-Control"));
            var cacheHeader = ctx.Response.Headers["Cache-Control"].ToString();
            Assert.StartsWith("public,", cacheHeader, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("max-age=31536000", cacheHeader);
            ctx.Response.Body.Position = 0;
            using var ms = new MemoryStream();
            ctx.Response.Body.CopyTo(ms);
            Assert.True(ms.Length >= bytes.Length);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
    }
}
