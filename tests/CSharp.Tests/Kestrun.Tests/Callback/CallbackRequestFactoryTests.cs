using Kestrun.Callback;
using Kestrun.Models;
using Serilog;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackRequestFactoryTests
{
    static CallbackRequestFactoryTests()
        => Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public void FromPlan_MergesExecutionPlanParameters_ResolvesUrl_AndBuildsIdempotencyKey()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.TraceIdentifier = "cid-123";
        });

        ctx.Parameters = new ResolvedRequestParameters(); // keep empty; token will come from execution plan

        var plan = new CallbackPlan(
            CallbackId: "paymentStatus",
            UrlTemplate: "https://hooks.example.com/v1/payments/{paymentId}/status",
            Method: HttpMethod.Post,
            OperationId: "paymentStatusCallback__post__status",
            PathParams: [],
            Body: new CallbackBodyPlan("application/json"));

        var exec = new CallBackExecutionPlan(
            CallbackId: plan.CallbackId,
            Plan: plan,
            BodyParameterName: "payload",
            Parameters: new Dictionary<string, object?>
            {
                ["paymentId"] = "p-001",
                ["payload"] = new { status = "ok" }
            });

        var resolver = new CapturingResolver();
        var serializer = new FixedBodySerializer("application/problem+json");

        var options = new CallbackDispatchOptions { DefaultTimeout = TimeSpan.FromSeconds(7) };

        var req = CallbackRequestFactory.FromPlan(exec, ctx, resolver, serializer, options);

        Assert.Equal("paymentStatus", req.CallbackId);
        Assert.Equal("paymentStatusCallback__post__status", req.OperationId);
        Assert.Equal("POST", req.HttpMethod);
        Assert.Equal(TimeSpan.FromSeconds(7), req.Timeout);

        Assert.Equal("application/problem+json", req.ContentType);
        Assert.NotNull(req.Body);

        Assert.Equal("cid-123", req.CorrelationId);

        // Ensures placeholder seed includes paymentId
        Assert.Contains("paymentId=p-001", req.IdempotencyKey);
        Assert.Contains(":paymentStatus:paymentStatusCallback__post__status", req.IdempotencyKey);

        Assert.True(req.Headers.ContainsKey("X-Kestrun-CallbackId"));
        Assert.Equal("paymentStatus", req.Headers["X-Kestrun-CallbackId"]);

        Assert.Equal(plan.UrlTemplate, resolver.SeenTemplate);
        Assert.True(resolver.SeenVars.TryGetValue("paymentId", out var v));
        Assert.Equal("p-001", v);
    }

    [Fact]
    public void FromPlan_SetsBodyNull_WhenNoBodyParameterProvided()
    {
        var ctx = TestRequestFactory.CreateContext(configureContext: http =>
        {
            http.TraceIdentifier = "cid-123";
        });

        var plan = new CallbackPlan(
            CallbackId: "cb",
            UrlTemplate: "https://hooks.example.com/v1/p/{paymentId}",
            Method: HttpMethod.Post,
            OperationId: "op",
            PathParams: [],
            Body: null);

        var exec = new CallBackExecutionPlan(
            CallbackId: plan.CallbackId,
            Plan: plan,
            BodyParameterName: null,
            Parameters: new Dictionary<string, object?>
            {
                ["paymentId"] = "p-001",
            });

        var req = CallbackRequestFactory.FromPlan(
            exec,
            ctx,
            new CapturingResolver(),
            new FixedBodySerializer("application/json"),
            new CallbackDispatchOptions());

        Assert.Null(req.Body);
    }

    private sealed class CapturingResolver : ICallbackUrlResolver
    {
        public string? SeenTemplate { get; private set; }
        public IReadOnlyDictionary<string, object?> SeenVars { get; private set; } =
            new Dictionary<string, object?>();

        public Uri Resolve(string urlTemplate, CallbackRuntimeContext ctx)
        {
            SeenTemplate = urlTemplate;
            SeenVars = new Dictionary<string, object?>(ctx.Vars, StringComparer.OrdinalIgnoreCase);
            return new Uri(urlTemplate, UriKind.Absolute);
        }
    }

    private sealed class FixedBodySerializer(string contentType) : ICallbackBodySerializer
    {
        private readonly string _contentType = contentType;

        public (string ContentType, byte[] Body) Serialize(CallbackPlan plan, CallbackRuntimeContext ctx)
            => (_contentType, Array.Empty<byte>());
    }
}
