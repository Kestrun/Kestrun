using Kestrun.OpenApi;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public class CallbackOperationIdTests
{
    [Theory]
    [InlineData(null, "get", "/orders")]
    [InlineData(" ", "get", "/orders")]
    [InlineData("cb", null, "/orders")]
    [InlineData("cb", " ", "/orders")]
    [InlineData("cb", "get", null)]
    [InlineData("cb", "get", " ")]
    public void From_InvalidInputs_ThrowsArgumentException(string? callbackName, string? httpVerb, string? pattern) => _ = Assert.Throws<ArgumentException>(() => CallbackOperationId.From(callbackName!, httpVerb!, pattern!));

    [Fact]
    public void From_NormalizesVerbAndPattern()
    {
        var result = CallbackOperationId.From("statusCb", "  POST ", " /order/status/{orderId} ");

        Assert.Equal("statusCb__post__order_status_orderId", result);
    }

    [Fact]
    public void From_ReplacesSymbolsAndCollapsesUnderscores()
    {
        var result = CallbackOperationId.From("cb", "get", "/orders/@special//{id}---done");

        Assert.Equal("cb__get__orders_special_id_done", result);
    }

    [Theory]
    [InlineData(null, "get", "/orders")]
    [InlineData(" ", "get", "/orders")]
    [InlineData("cb", null, "/orders")]
    [InlineData("cb", " ", "/orders")]
    [InlineData("cb", "get", null)]
    [InlineData("cb", "get", " ")]
    public void FromLastSegment_InvalidInputs_ThrowsArgumentException(string? callbackName, string? httpVerb, string? pattern) => _ = Assert.Throws<ArgumentException>(() => CallbackOperationId.FromLastSegment(callbackName!, httpVerb!, pattern!));

    [Fact]
    public void FromLastSegment_UsesLastNonParameterSegment()
    {
        var result = CallbackOperationId.FromLastSegment("statusCb", "PUT", "/orders/status/{id}");

        Assert.Equal("statusCb__put__status", result);
    }

    [Fact]
    public void FromLastSegment_AllParameterSegments_FallsBackToTrimmedLast()
    {
        var result = CallbackOperationId.FromLastSegment("statusCb", "PUT", "/{tenant}/{id}");

        Assert.Equal("statusCb__put__id", result);
    }

    [Fact]
    public void FromLastSegment_EmptyPatternBody_UsesDefaultOp()
    {
        var result = CallbackOperationId.FromLastSegment("statusCb", "GET", "/");

        Assert.Equal("statusCb__get__op", result);
    }

    [Theory]
    [InlineData(null, "/orders")]
    [InlineData(" ", "/orders")]
    [InlineData("$request.body#/url", null)]
    [InlineData("$request.body#/url", " ")]
    public void BuildCallbackKey_InvalidInputs_ThrowsArgumentException(string? expression, string? pattern) => _ = Assert.Throws<ArgumentException>(() => CallbackOperationId.BuildCallbackKey(expression!, pattern!));

    [Fact]
    public void BuildCallbackKey_UnbracedExpression_NormalizesAndPrefixesPatternSlash()
    {
        var result = CallbackOperationId.BuildCallbackKey("request.body#/callbackUrls/status", "status");

        Assert.Equal("{$request.body#/callbackUrls/status}/status", result.ToString());
    }

    [Fact]
    public void BuildCallbackKey_BracedExpressionWithoutDollar_AddsDollar()
    {
        var result = CallbackOperationId.BuildCallbackKey("{request.body#/callbackUrls/status}", "/events");

        Assert.Equal("{$request.body#/callbackUrls/status}/events", result.ToString());
    }

    [Fact]
    public void BuildCallbackKey_MalformedBracedExpressionEndingWithSlash_AvoidsDoubleSlash()
    {
        var result = CallbackOperationId.BuildCallbackKey("{request.body#/callbackUrls/status/", "/events");

        Assert.Equal("{$request.body#/callbackUrls/status/events", result.ToString());
    }
}
