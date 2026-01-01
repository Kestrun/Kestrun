using System.Net;

namespace KestrunTests.Callback;

/// <summary>
/// HTTP message handler that captures sent requests for inspection.
/// </summary>
/// <param name="handler"></param>
internal sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapturingHttpMessageHandler"/> class.
    /// </summary>
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

    /// <summary>
    /// Gets a task that completes when an HTTP request is sent.
    /// </summary>
    public TaskCompletionSource<HttpRequestMessage> SeenRequest { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Sends an HTTP request asynchronously and captures the request.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the HTTP response message.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = SeenRequest.TrySetResult(request);
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a CapturingHttpMessageHandler that always responds with the specified status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <returns>A CapturingHttpMessageHandler that always responds with the specified status code.</returns>
    public static CapturingHttpMessageHandler Respond(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        return new((_, __) => Task.FromResult(response));
    }
}

/// <summary>
/// A fake HTTP client factory that always returns the same HttpClient instance.
/// </summary>
/// <param name="client">The HttpClient instance to return.</param>
internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeHttpClientFactory"/> class.
    /// </summary>
    private readonly HttpClient _client = client;

    /// <summary>
    /// Creates an HttpClient with the specified name.
    /// </summary>
    /// <param name="name">The name of the HttpClient to create.</param>
    /// <returns>The HttpClient instance.</returns>
    public HttpClient CreateClient(string name) => _client;
}
