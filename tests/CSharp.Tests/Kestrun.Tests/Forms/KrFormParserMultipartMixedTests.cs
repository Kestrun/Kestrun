using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

    [Trait("Category", "Forms")]
    [Fact]
    public async Task ParseMultipartMixed_ScopedRulesApplyOnlyToMatchingScope()
    {
        var outer = "outer-boundary";
        var inner = "inner-boundary";
        var innerBody = string.Join("\r\n", new[]
        {
            $"--{inner}",
            "Content-Disposition: form-data; name=\"text\"",
            "Content-Type: text/plain",
            string.Empty,
            "inner",
            $"--{inner}--",
            string.Empty
        });

        var outerBody = string.Join("\r\n", new[]
        {
            $"--{outer}",
            "Content-Disposition: form-data; name=\"nested\"",
            $"Content-Type: multipart/mixed; boundary={inner}",
            string.Empty,
            innerBody,
            $"--{outer}--",
            string.Empty
        });

        var context = CreateContext(outerBody, outer);
        var options = new KrFormOptions
        {
            Logger = new LoggerConfiguration().CreateLogger()
        };

        options.AllowedRequestContentTypes.Add("multipart/mixed");
        options.Rules.Add(new KrFormPartRule { Name = "text", StoreToDisk = true });
        options.Rules.Add(new KrFormPartRule { Name = "text", Scope = "nested", StoreToDisk = false });

        var payload = await KrFormParser.ParseAsync(context, options, CancellationToken.None);
        var multipart = Assert.IsType<KrMultipart>(payload);
        var nestedContainer = multipart.Parts.Single(part => string.Equals(part.Name, "nested", StringComparison.OrdinalIgnoreCase));
        var nestedPayload = Assert.IsType<KrMultipart>(nestedContainer.NestedPayload);
        var nestedText = nestedPayload.Parts.Single(part => string.Equals(part.Name, "text", StringComparison.OrdinalIgnoreCase));

        Assert.True(string.IsNullOrWhiteSpace(nestedText.TempPath));

        if (!string.IsNullOrWhiteSpace(nestedContainer.TempPath) && File.Exists(nestedContainer.TempPath))
        {
            File.Delete(nestedContainer.TempPath);
        }
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
