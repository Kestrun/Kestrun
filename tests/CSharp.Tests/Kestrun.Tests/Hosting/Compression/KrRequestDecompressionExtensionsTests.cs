using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using Kestrun.Hosting;
using Kestrun.Hosting.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Moq;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Hosting.Compression;

public class KrRequestDecompressionExtensionsTests
{
    [Fact]
    [Trait("Category", "Compression")]
    public async Task AddRequestDecompression_WithAllowedEncodings_RejectsUnsupportedEncoding()
    {
        var hostLogger = new Mock<Serilog.ILogger>();
        _ = hostLogger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);

        using var host = new KestrunHost("TestServer", hostLogger.Object);
        _ = host.Builder.WebHost.UseTestServer();

        _ = host.AddRequestDecompression(["gzip"]);

        var app = host.Build();
        _ = app.MapPost("/echo", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var text = await reader.ReadToEndAsync(ctx.RequestAborted);
            return Results.Text(text);
        });

        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();

            using var req = new HttpRequestMessage(HttpMethod.Post, "/echo")
            {
                Content = new StringContent("hello")
            };
            req.Content.Headers.ContentEncoding.Add("br");
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            using var resp = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("Unsupported Content-Encoding.", body);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    [Trait("Category", "Compression")]
    public async Task AddRequestDecompression_WithAllowedEncodings_AllowsGzip_AndDecompressesBody()
    {
        var hostLogger = new Mock<Serilog.ILogger>();
        _ = hostLogger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);

        using var host = new KestrunHost("TestServer", hostLogger.Object);
        _ = host.Builder.WebHost.UseTestServer();

        _ = host.AddRequestDecompression(["gzip"]);

        var app = host.Build();
        _ = app.MapPost("/echo", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var text = await reader.ReadToEndAsync(ctx.RequestAborted);
            return Results.Text(text);
        });

        await app.StartAsync();
        try
        {
            var client = app.GetTestClient();

            const string message = "hello world";
            var compressed = Gzip(message);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/echo")
            {
                Content = new ByteArrayContent(compressed)
            };
            req.Content.Headers.ContentEncoding.Add("gzip");
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            using var resp = await client.SendAsync(req);
            _ = resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync();
            Assert.Equal(message, body);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static byte[] Gzip(string text)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(input, 0, input.Length);
        }
        return output.ToArray();
    }
}
