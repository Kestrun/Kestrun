using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Kestrun.Client;
using Xunit;

namespace Kestrun.Tests.Client;

public class KrHttpClientAndDownloadTests
{
    [Fact]
    public void CreateTcpClient_AppliesBaseAddressAndTimeout()
    {
        var client = KrHttpClientFactory.CreateTcpClient(
            new Uri("https://example.test"),
            new KrHttpClientOptions { Timeout = TimeSpan.FromSeconds(7) });

        Assert.Equal(new Uri("https://example.test"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
    }

    [Fact]
    public void CreateTcpClient_UsesDefaultTimeoutForNonPositiveValue()
    {
        var client = KrHttpClientFactory.CreateTcpClient(
            new Uri("https://example.test"),
            new KrHttpClientOptions { Timeout = TimeSpan.Zero });

        Assert.Equal(TimeSpan.FromSeconds(100), client.Timeout);
    }

    [Fact]
    public void CreateNamedPipeClient_ThrowsOnBlankPipeName()
    {
        _ = Assert.Throws<ArgumentNullException>(() => KrHttpClientFactory.CreateNamedPipeClient(" ", new KrHttpClientOptions()));
    }

    [Fact]
    public void CreateUnixSocketClient_ThrowsOnBlankPath()
    {
        _ = Assert.Throws<ArgumentNullException>(() => KrHttpClientFactory.CreateUnixSocketClient("", new KrHttpClientOptions()));
    }

    [Fact]
    public async Task DownloadToFileAsync_WritesContentAndReturnsLength()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello world", Encoding.UTF8),
            }));

        var targetPath = Path.Combine(Path.GetTempPath(), $"kestrun-download-{Guid.NewGuid():N}.txt");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/download");
            var finalLength = await KrHttpDownloads.DownloadToFileAsync(client, request, targetPath, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(new FileInfo(targetPath).Length, finalLength);
            Assert.Equal("hello world", await File.ReadAllTextAsync(targetPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public async Task DownloadToFileAsync_WithResume_AppendsAndSetsRangeHeader()
    {
        RangeHeaderValue? observedRange = null;

        using var client = new HttpClient(new StubHandler(request =>
        {
            observedRange = request.Headers.Range;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("def", Encoding.UTF8),
            };
        }));

        var targetPath = Path.Combine(Path.GetTempPath(), $"kestrun-download-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllBytesAsync(targetPath, Encoding.ASCII.GetBytes("abc"), TestContext.Current.CancellationToken);
            var initialLength = new FileInfo(targetPath).Length;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/download");
            var finalLength = await KrHttpDownloads.DownloadToFileAsync(client, request, targetPath, resume: true, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(observedRange);
            Assert.Equal(initialLength, observedRange!.Ranges.First().From);
            Assert.Equal("abcdef", await File.ReadAllTextAsync(targetPath, TestContext.Current.CancellationToken));
            Assert.Equal(initialLength + 3, finalLength);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
