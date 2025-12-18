using Kestrun.Health;
using Xunit;
using System.Net;

namespace KestrunTests.Health;

public class HttpProbeTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    [Fact]
    public async Task HttpProbe_ParsesContractJson()
    {
        var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(/*lang=json,strict*/ "{\"status\":\"healthy\",\"description\":\"ok\"}")
        }));
        var probe = new HttpProbe("http-h", ["live"], http, "http://unit-test/health");
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Healthy, result.Status);
        Assert.Equal(ProbeStatusLabels.STATUS_OK, result.Description);
    }

    [Fact]
    public async Task HttpProbe_NonContract200_IsDegraded()
    {
        var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json content")
        }));
        var probe = new HttpProbe("http-d", ["live"], http, "http://unit-test/health");
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Degraded, result.Status);
        Assert.Contains("No contract", result.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpProbe_ServerError_IsUnhealthy()
    {
        var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops")
        }));
        var probe = new HttpProbe("http-u", ["live"], http, "http://unit-test/health");
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Unhealthy, result.Status);
        Assert.Equal("HTTP 500", result.Description);
    }

    [Fact]
    public async Task HttpProbe_Exception_IsUnhealthy()
    {
        var http = new HttpClient(new FaultHandler());
        // Use a generous timeout here: this test is specifically about the exception path.
        // Very small timeouts can race into the timeout handler on loaded CI agents before the handler executes.
        var probe = new HttpProbe("http-x", ["live"], http, "http://unit-test/health", TimeSpan.FromSeconds(5));
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Unhealthy, result.Status);
        Assert.Contains("Exception:", result.Description);
    }

    [Fact]
    public async Task HttpProbe_Timeout_IsDegraded()
    {
        var http = new HttpClient(new SlowHandler());
        var probe = new HttpProbe("http-t", ["live"], http, "http://unit-test/health", TimeSpan.FromMilliseconds(50));
        var result = await probe.CheckAsync();
        Assert.Equal(ProbeStatus.Degraded, result.Status);
        Assert.Contains("Timeout", result.Description);
    }

    private sealed class FaultHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
