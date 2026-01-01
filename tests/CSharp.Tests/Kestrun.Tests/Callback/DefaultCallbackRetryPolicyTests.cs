using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class DefaultCallbackRetryPolicyTests
{
    [Fact]
    public void Evaluate_RetriesOnHttpRequestException_ByDefault()
    {
        var policy = new DefaultCallbackRetryPolicy(options: null);

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

        var res = new CallbackResult(false, null, "HttpRequestException", "boom", DateTimeOffset.UtcNow);

        var decision = policy.Evaluate(req, res);

        Assert.Equal(RetryDecisionKind.Retry, decision.Kind);
        Assert.InRange(decision.Delay, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2.25));
    }

    [Fact]
    public void Evaluate_StopsWhenMaxAttemptsReached()
    {
        var policy = new DefaultCallbackRetryPolicy(new CallbackDispatchOptions { MaxAttempts = 3 });

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
            timeout: TimeSpan.FromSeconds(1))
        {
            // attempt 2 -> nextAttemptNumber = 3 -> stop
            Attempt = 2
        };

        var res = new CallbackResult(false, 500, "HttpError", "err", DateTimeOffset.UtcNow);

        var decision = policy.Evaluate(req, res);

        Assert.Equal(RetryDecisionKind.Stop, decision.Kind);
        Assert.Equal(TimeSpan.Zero, decision.Delay);
        Assert.Equal("MaxAttemptsReached", decision.Reason);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public void Evaluate_StopsOnPermanentFailures(int statusCode)
    {
        var policy = new DefaultCallbackRetryPolicy(new CallbackDispatchOptions { MaxAttempts = 10 });

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

        var res = new CallbackResult(false, statusCode, "HttpError", "err", DateTimeOffset.UtcNow);

        var decision = policy.Evaluate(req, res);

        Assert.Equal(RetryDecisionKind.Stop, decision.Kind);
        Assert.Equal("PermanentFailure", decision.Reason);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(408)]
    [InlineData(429)]
    public void Evaluate_RetriesOnRetryableStatusCodes(int statusCode)
    {
        var policy = new DefaultCallbackRetryPolicy(new CallbackDispatchOptions { MaxAttempts = 10 });

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

        var res = new CallbackResult(false, statusCode, "HttpError", "err", DateTimeOffset.UtcNow);

        var decision = policy.Evaluate(req, res);

        Assert.Equal(RetryDecisionKind.Retry, decision.Kind);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(302)]
    [InlineData(418)]
    public void Evaluate_StopsOnNotRetryableStatusCodes(int statusCode)
    {
        var policy = new DefaultCallbackRetryPolicy(new CallbackDispatchOptions { MaxAttempts = 10 });

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

        var res = new CallbackResult(false, statusCode, "HttpError", "err", DateTimeOffset.UtcNow);

        var decision = policy.Evaluate(req, res);

        Assert.Equal(RetryDecisionKind.Stop, decision.Kind);
        Assert.Equal("NotRetryable", decision.Reason);
    }
}
