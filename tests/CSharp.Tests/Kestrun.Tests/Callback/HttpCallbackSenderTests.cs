using System.Net;
using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class HttpCallbackSenderTests
{
    [Fact]
    public async Task SendAsync_SendsExpectedRequest_AndReturnsSuccessOn2xx()
    {
        var handler = CapturingHttpMessageHandler.Respond(HttpStatusCode.Created);
        var http = new HttpClient(handler);

        var sender = new HttpCallbackSender(http);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com/callback"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Custom"] = "v"
            },
            contentType: "application/json",
            body: System.Text.Encoding.UTF8.GetBytes("{}"),
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromSeconds(5));

        var res = await sender.SendAsync(req, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal((int)HttpStatusCode.Created, res.StatusCode);

        var seen = await handler.SeenRequest.Task;
        Assert.Equal("POST", seen.Method.Method);
        Assert.Equal(req.TargetUrl, seen.RequestUri);

        Assert.True(seen.Headers.TryGetValues("X-Custom", out var custom));
        Assert.Equal("v", custom.Single());

        Assert.True(seen.Headers.TryGetValues("X-Correlation-Id", out var cid));
        Assert.Equal("cid", cid.Single());

        Assert.True(seen.Headers.TryGetValues("Idempotency-Key", out var idk));
        Assert.Equal("idk", idk.Single());

        Assert.NotNull(seen.Content);
        Assert.Equal("application/json", seen.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SendAsync_ReturnsHttpError_OnNon2xx()
    {
        var handler = CapturingHttpMessageHandler.Respond(HttpStatusCode.InternalServerError);
        var http = new HttpClient(handler);

        var sender = new HttpCallbackSender(http);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com/callback"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            body: null,
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromSeconds(5));

        var res = await sender.SendAsync(req, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal((int)HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal("HttpError", res.ErrorType);
    }

    [Fact]
    public async Task SendAsync_ReturnsHttpRequestException_WhenHandlerThrows()
    {
        var handler = new CapturingHttpMessageHandler((_, __) => throw new HttpRequestException("boom"));
        var http = new HttpClient(handler);

        var sender = new HttpCallbackSender(http);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com/callback"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            body: null,
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromSeconds(5));

        var res = await sender.SendAsync(req, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Null(res.StatusCode);
        Assert.Equal("HttpRequestException", res.ErrorType);
    }

    [Fact]
    public async Task SendAsync_ReturnsTimeout_WhenRequestTimeoutElapses()
    {
        var handler = new CapturingHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var http = new HttpClient(handler);
        var sender = new HttpCallbackSender(http);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com/callback"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            body: null,
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromMilliseconds(50));

        var res = await sender.SendAsync(req, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Null(res.StatusCode);
        Assert.Equal("Timeout", res.ErrorType);
    }

    [Fact]
    public async Task SendAsync_InvokesSigner_WhenProvided()
    {
        var handler = CapturingHttpMessageHandler.Respond(HttpStatusCode.OK);
        var http = new HttpClient(handler);

        var signer = new TestSigner();
        var sender = new HttpCallbackSender(http, signer);

        var req = new CallbackRequest(
            callbackId: "cb",
            operationId: "op",
            targetUrl: new Uri("https://example.com/callback"),
            httpMethod: "POST",
            headers: new Dictionary<string, string>(),
            contentType: "application/json",
            body: System.Text.Encoding.UTF8.GetBytes("{}"),
            correlationId: "cid",
            idempotencyKey: "idk",
            timeout: TimeSpan.FromSeconds(5));

        var res = await sender.SendAsync(req, CancellationToken.None);
        Assert.True(res.Success);
        Assert.True(signer.Called);

        var seen = await handler.SeenRequest.Task;
        Assert.True(seen.Headers.Contains("X-Test-Signed"));
    }

    private sealed class TestSigner : ICallbackSigner
    {
        public bool Called { get; private set; }

        public void Sign(HttpRequestMessage message, CallbackRequest request)
        {
            Called = true;
            _ = message.Headers.TryAddWithoutValidation("X-Test-Signed", "1");
        }
    }
}
