using System.Text.Json;
using Kestrun.Callback;
using Xunit;

namespace KestrunTests.Callback;

public class JsonCallbackBodySerializerTests
{
    [Fact]
    public void Serialize_ReturnsEmpty_WhenPlanHasNoBody()
    {
        var s = new JsonCallbackBodySerializer();

        var plan = new CallbackPlan(
            CallbackId: "cb",
            UrlTemplate: "https://example.com",
            Method: HttpMethod.Post,
            OperationId: "op",
            PathParams: [],
            Body: null);

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(),
            CallbackPayload: new { a = 1 });

        var (ct, body) = s.Serialize(plan, ctx);

        Assert.Equal("application/json", ct);
        Assert.Empty(body);
    }

    [Fact]
    public void Serialize_ReturnsEmpty_WhenPayloadIsNull()
    {
        var s = new JsonCallbackBodySerializer();

        var plan = new CallbackPlan(
            CallbackId: "cb",
            UrlTemplate: "https://example.com",
            Method: HttpMethod.Post,
            OperationId: "op",
            PathParams: [],
            Body: new CallbackBodyPlan(MediaType: "application/problem+json"));

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(),
            CallbackPayload: null);

        var (ct, body) = s.Serialize(plan, ctx);

        Assert.Equal("application/problem+json", ct);
        Assert.Empty(body);
    }

    [Fact]
    public void Serialize_SerializesPayload_AsUtf8JsonBytes()
    {
        var s = new JsonCallbackBodySerializer();

        var plan = new CallbackPlan(
            CallbackId: "cb",
            UrlTemplate: "https://example.com",
            Method: HttpMethod.Post,
            OperationId: "op",
            PathParams: [],
            Body: new CallbackBodyPlan(MediaType: "application/json"));

        var payload = new { name = "alice", age = 30 };

        var ctx = new CallbackRuntimeContext(
            CorrelationId: "cid",
            IdempotencyKeySeed: "seed",
            DefaultBaseUri: null,
            Vars: new Dictionary<string, object?>(),
            CallbackPayload: payload);

        var (ct, body) = s.Serialize(plan, ctx);

        Assert.Equal("application/json", ct);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal("alice", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(30, doc.RootElement.GetProperty("age").GetInt32());
    }
}
