using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class DefaultCallbackUrlResolverTests
{
    [Fact]
    public void Resolve_ReplacesRuntimeExpressionAndTokens_AndEscapesTokens()
    {
        var resolver = new DefaultCallbackUrlResolver();

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["paymentId"] = "a b"
            },
            CallbackPayload: new
            {
                callbackUrls = new
                {
                    status = "https://hooks.example.com"
                }
            }
        );

        var url = resolver.Resolve("{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status", ctx);

        Assert.Equal("https://hooks.example.com/v1/payments/a%20b/status", url.AbsoluteUri);
    }

    [Fact]
    public void Resolve_ThrowsWhenPayloadIsNullButRuntimeExprUsed()
    {
        var resolver = new DefaultCallbackUrlResolver();

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            CallbackPayload: null
        );

        var ex = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve("{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status", ctx));

        Assert.Contains("request body is null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ThrowsWhenTokenMissing()
    {
        var resolver = new DefaultCallbackUrlResolver();

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            CallbackPayload: new { callbackUrls = new { status = "https://hooks.example.com" } }
        );

        var ex = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve("{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status", ctx));

        Assert.Contains("requires token 'paymentId'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_CombinesRelativeUrlsWithDefaultBaseUri()
    {
        var resolver = new DefaultCallbackUrlResolver();

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: new Uri("https://base.example.com"),
            Vars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["paymentId"] = "p1"
            },
            CallbackPayload: new { }
        );

        var url = resolver.Resolve("/v1/payments/{paymentId}", ctx);

        Assert.Equal("https://base.example.com/v1/payments/p1", url.AbsoluteUri);
    }

    [Fact]
    public void Resolve_ThrowsForRelativeUrlWhenDefaultBaseUriMissing()
    {
        var resolver = new DefaultCallbackUrlResolver();

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["paymentId"] = "p1"
            },
            CallbackPayload: new { }
        );

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("/v1/payments/{paymentId}", ctx));
        Assert.Contains("DefaultBaseUri is null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
