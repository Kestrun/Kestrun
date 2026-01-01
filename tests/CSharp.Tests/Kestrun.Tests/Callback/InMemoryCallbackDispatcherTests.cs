using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class InMemoryCallbackDispatcherTests
{
    [Fact]
    public async Task EnqueueAsync_WritesToQueue()
    {
        var queue = new InMemoryCallbackQueue();
        var dispatcher = new InMemoryCallbackDispatcher(queue);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            body: null,
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromSeconds(1));

        await dispatcher.EnqueueAsync(req);

        var got = await queue.Channel.Reader.ReadAsync();
        Assert.Same(req, got);
    }
}
