using System.Management.Automation;
using Kestrun.Callback;
using Kestrun.Languages;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackRuntimeContextFactoryTests
{
    [Fact]
    public void FromHttpContext_PopulatesVarsAndPayload_AndBuildsIdempotencySeedFromTemplateParams()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.TraceIdentifier = "cid-123";
        });

        ctx.Parameters.Parameters["paymentId"] = CreateResolvedPathParam("paymentId", "p-001");
        ctx.Parameters.Body = CreateResolvedBody("body", new { callbackUrls = new { status = "https://hooks.example.com" } });

        var rt = CallbackRuntimeContextFactory.FromHttpContext(ctx, urlTemplate: "/v1/payments/{paymentId}");

        Assert.Equal("cid-123", rt.CorrelationId);
        Assert.Equal("paymentId=p-001", rt.IdempotencyKeySeed);

        Assert.True(rt.Vars.TryGetValue("paymentId", out var v));
        Assert.Equal("p-001", v);

        Assert.NotNull(rt.CallbackPayload);
    }

    [Fact]
    public void FromHttpContext_FallsBackIdempotencySeedToCorrelationId_WhenNoTemplateProvided()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.TraceIdentifier = "cid-xyz";
        });

        ctx.Parameters.Parameters["paymentId"] = CreateResolvedPathParam("paymentId", "p-001");

        var rt = CallbackRuntimeContextFactory.FromHttpContext(ctx, urlTemplate: null);

        Assert.Equal("cid-xyz", rt.IdempotencyKeySeed);
    }

    [Fact]
    public void FromHttpContext_FallsBackIdempotencySeedToCorrelationId_WhenTemplateHasNoResolvedValues()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.TraceIdentifier = "cid-empty";
        });

        // paymentId exists but is null -> should not contribute to seed
        ctx.Parameters.Parameters["paymentId"] = CreateResolvedPathParam("paymentId", null);

        var rt = CallbackRuntimeContextFactory.FromHttpContext(ctx, urlTemplate: "/v1/payments/{paymentId}");

        Assert.Equal("cid-empty", rt.IdempotencyKeySeed);
    }

    private static ParameterForInjectionResolved CreateResolvedPathParam(string name, object? value)
    {
        var pm = new ParameterMetadata(name, typeof(string));
        var oap = new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Path,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };

        var info = new ParameterForInjectionInfo(pm, oap);
        return new ParameterForInjectionResolved(info, value);
    }

    private static ParameterForInjectionResolved CreateResolvedBody(string name, object? value)
    {
        var pm = new ParameterMetadata(name, typeof(object));

        // For these unit tests we only need a resolved "body" value to flow into ctx.Parameters.Body.
        // Creating a full OpenApiRequestBody is brittle across OpenAPI package versions because Content
        // may be null or read-only. Using an OpenApiParameter with In=null behaves as "request body"
        // for Kestrun's injection model.
        var bodyAsParameter = new OpenApiParameter
        {
            Name = name,
            In = null,
            Schema = new OpenApiSchema { Type = JsonSchemaType.Object }
        };

        var info = new ParameterForInjectionInfo(pm, bodyAsParameter);
        return new ParameterForInjectionResolved(info, value);
    }
}
