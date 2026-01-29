using System.Text;
using Kestrun.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit;

namespace KestrunTests.Forms;

public class KrFormParserMultipartMixedTests
{
    [Trait("Category", "Forms")]
    [Fact]
    public async Task ParseMultipartMixed_WithoutContentDisposition_ThrowsBadRequest()
    {
        var boundary = "mixed-boundary";
        var body = string.Join("\r\n", new[]
        {
            $"--{boundary}",
            "Content-Type: text/plain",
            string.Empty,
            "first",
            $"--{boundary}",
            "Content-Type: application/json",
            string.Empty,
            /*lang=json,strict*/
                                 "{\"value\":42}",
            $"--{boundary}--",
            string.Empty
        });

        var context = CreateContext(body, boundary);
        var options = CreateOptions();

        var ex = await Assert.ThrowsAsync<KrFormException>(() =>
            KrFormParser.ParseAsync(context, options, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [Trait("Category", "Forms")]
    [Fact]
    public async Task ParseMultipartMixed_WithContentDisposition_ReturnsOrderedParts()
    {
        var boundary = "mixed-boundary";
        var body = string.Join("\r\n", new[]
        {
            $"--{boundary}",
            "Content-Disposition: form-data; name=\"text\"",
            "Content-Type: text/plain",
            string.Empty,
            "first",
            $"--{boundary}",
            "Content-Disposition: form-data; name=\"json\"",
            "Content-Type: application/json",
            string.Empty,
            /*lang=json,strict*/
                                 "{\"value\":42}",
            $"--{boundary}--",
            string.Empty
        });

        var context = CreateContext(body, boundary);
        var options = CreateOptions();

        var payload = await KrFormParser.ParseAsync(context, options, CancellationToken.None);
        var multipart = Assert.IsType<KrMultipart>(payload);

        Assert.Equal(2, multipart.Parts.Count);
        Assert.Equal("text/plain", multipart.Parts[0].ContentType);
        Assert.Equal("application/json", multipart.Parts[1].ContentType);
    }

    [Trait("Category", "Forms")]
    [Fact]
    public async Task ParseMultipartMixed_WithoutBoundary_ThrowsBadRequest()
    {
        var boundary = "mixed-boundary";
        var body = string.Join("\r\n", new[]
        {
            $"--{boundary}",
            "Content-Disposition: form-data; name=\"text\"",
            "Content-Type: text/plain",
            string.Empty,
            "first",
            $"--{boundary}--",
            string.Empty
        });

        var context = CreateContext(body, boundary, includeBoundary: false);
        var options = CreateOptions();

        var ex = await Assert.ThrowsAsync<KrFormException>(() =>
            KrFormParser.ParseAsync(context, options, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    private static DefaultHttpContext CreateContext(string body, string boundary, bool includeBoundary = true)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = includeBoundary
            ? $"multipart/mixed; boundary={boundary}"
            : "multipart/mixed";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;

        return context;
    }

    private static KrFormOptions CreateOptions()
    {
        var options = new KrFormOptions
        {
            Logger = new LoggerConfiguration().CreateLogger()
        };

        options.AllowedRequestContentTypes.Add("multipart/mixed");
        options.Rules.Add(new KrFormPartRule { Name = "text", StoreToDisk = false });
        options.Rules.Add(new KrFormPartRule { Name = "json", StoreToDisk = false });

        return options;
    }
}
