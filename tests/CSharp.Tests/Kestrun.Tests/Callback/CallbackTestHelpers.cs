using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KestrunTests.Callback;

internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public TaskCompletionSource<HttpRequestMessage> SeenRequest { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SeenRequest.TrySetResult(request);
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    public static CapturingHttpMessageHandler Respond(HttpStatusCode statusCode)
        => new((_, __) => Task.FromResult(new HttpResponseMessage(statusCode)));
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public FakeHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public HttpClient CreateClient(string name) => _client;
}
