using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class InMemoryCallbackQueueTests
{
    [Fact]
    public async Task Channel_AllowsWriteAndRead()
    {
        var queue = new InMemoryCallbackQueue();

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

        await queue.Channel.Writer.WriteAsync(req);

        var got = await queue.Channel.Reader.ReadAsync();
        Assert.Same(req, got);
    }
}
