using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackModelsSmokeTests
{
    [Fact]
    public void CallbackRequest_SetsDefaults()
    {
        var now = DateTimeOffset.UtcNow;

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

        Assert.Equal(0, req.Attempt);
        Assert.True(req.CreatedAt >= now);
        Assert.Equal(req.CreatedAt, req.NextAttemptAt);
    }

    [Fact]
    public void CallbackResult_IsRecordWithExpectedFields()
    {
        var completedAt = DateTimeOffset.UtcNow;
        var res = new CallbackResult(false, 500, "HttpError", "oops", completedAt);

        Assert.False(res.Success);
        Assert.Equal(500, res.StatusCode);
        Assert.Equal("HttpError", res.ErrorType);
        Assert.Equal("oops", res.ErrorMessage);
        Assert.Equal(completedAt, res.CompletedAt);
    }

    [Fact]
    public void RetryDecision_IsRecordWithExpectedFields()
    {
        var next = DateTimeOffset.UtcNow;
        var d = new RetryDecision(RetryDecisionKind.Retry, next, TimeSpan.FromSeconds(1), "why");

        Assert.Equal(RetryDecisionKind.Retry, d.Kind);
        Assert.Equal(next, d.NextAttemptAt);
        Assert.Equal(TimeSpan.FromSeconds(1), d.Delay);
        Assert.Equal("why", d.Reason);
    }

    [Fact]
    public void CallbackRuntimeContext_IsRecordWithExpectedFields()
    {
        var vars = new Dictionary<string, object?> { ["x"] = 1 };
        var payload = new { a = 1 };

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: new Uri("https://example.com"),
            Vars: vars,
            CallbackPayload: payload);

        Assert.Equal("cid", ctx.CorrelationId);
        Assert.Equal("seed", ctx.IdempotencyKeySeed);
        Assert.Equal(vars, ctx.Vars);
        Assert.Equal(payload, ctx.CallbackPayload);
    }
}
