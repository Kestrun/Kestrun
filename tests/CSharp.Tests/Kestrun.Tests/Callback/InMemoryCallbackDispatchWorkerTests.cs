using System.Net;
using System.Text;
using Kestrun.Callback;
using Serilog;
using Xunit;

namespace KestrunTests.Callback;

public class InMemoryCallbackDispatchWorkerTests
{
    static InMemoryCallbackDispatchWorkerTests()
        => Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public async Task Worker_ConsumesQueue_AndSendsHttpRequest()
    {
        var queue = new InMemoryCallbackQueue();

        using var handler = new CapturingHttpMessageHandler((req, _) =>
          {
              // We only care that the request is constructed correctly.
              return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
          });

        var http = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(http);

        var worker = new InMemoryCallbackDispatchWorker(queue, factory, Log.Logger);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var callback = new CallbackRequest(
                callbackId: "cb",
                operationId: "op",
                targetUrl: new Uri("https://example.com/cb"),
                httpMethod: "POST",
                headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-Custom"] = "v"
                },
                contentType: "application/json",
                body: Encoding.UTF8.GetBytes("{}"),
                correlationId: "cid",
                idempotencyKey: "idk",
                timeout: TimeSpan.FromSeconds(5));

            await queue.Channel.Writer.WriteAsync(callback);

            var seen = await handler.SeenRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("POST", seen.Method.Method);
            Assert.Equal(callback.TargetUrl, seen.RequestUri);

            Assert.True(seen.Headers.TryGetValues("X-Custom", out var custom));
            Assert.Equal("v", custom.Single());

            Assert.NotNull(seen.Content);
            Assert.Equal("application/json", seen.Content!.Headers.ContentType!.MediaType);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
