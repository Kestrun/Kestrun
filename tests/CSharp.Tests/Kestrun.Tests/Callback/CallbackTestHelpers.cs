using System.Net;

namespace KestrunTests.Callback;

internal sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

    public TaskCompletionSource<HttpRequestMessage> SeenRequest { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = SeenRequest.TrySetResult(request);
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    public static CapturingHttpMessageHandler Respond(HttpStatusCode statusCode)
        => new((_, __) => Task.FromResult(new HttpResponseMessage(statusCode)));
}

internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    private readonly HttpClient _client = client;

    public HttpClient CreateClient(string name) => _client;
}
