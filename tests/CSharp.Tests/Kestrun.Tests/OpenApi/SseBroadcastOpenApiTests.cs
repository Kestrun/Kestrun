using Kestrun.Hosting;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class SseBroadcastOpenApiTests
{
    [Fact]
    public void GenerateDoc_IncludesSseBroadcastPath_WithTextEventStream()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        // OpenApiDocDescriptor.GenerateDoc() will also generate global components.
        // Some existing OpenAPI unit-test component types reference these examples by ID.
        var d = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample(
            "UserEx",
            new OpenApiExample
            {
                Summary = "User example",
                Value = OpenApiDocDescriptor.ToNode(new { Name = "Alice", Age = 42 })
            });
        d.AddComponentExample(
            "AlicePayload",
            new OpenApiExample
            {
                Summary = "Alice payload",
                Value = OpenApiDocDescriptor.ToNode(new { Name = "Alice" })
            });
        Kestrun.Hosting.Options.SseBroadcastOptions options = new()
        {
            Path = "/sse/broadcast",
            KeepAliveSeconds = 0
        };

        _ = host.AddSseBroadcast(options);

        Assert.True(host.RegisteredRoutes.TryGetValue(("/sse/broadcast", HttpVerb.Get), out var routeOptions));
        Assert.NotNull(routeOptions);
        Assert.NotNull(routeOptions.OpenAPI);
        Assert.True(routeOptions.OpenAPI.ContainsKey(HttpVerb.Get));

        d.GenerateDoc();

        var json = d.ToJson(OpenApiSpecVersion.OpenApi3_1);
        Assert.Contains("\"/sse/broadcast\"", json, StringComparison.Ordinal);
        Assert.Contains("text/event-stream", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateDoc_AllowsCustomizingSseBroadcastOpenApiMetadata()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        var d = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);
        d.AddComponentExample(
            "UserEx",
            new OpenApiExample
            {
                Summary = "User example",
                Value = OpenApiDocDescriptor.ToNode(new { Name = "Alice", Age = 42 })
            });
        d.AddComponentExample(
            "AlicePayload",
            new OpenApiExample
            {
                Summary = "Alice payload",
                Value = OpenApiDocDescriptor.ToNode(new { Name = "Alice" })
            });
        Kestrun.Hosting.Options.SseBroadcastOptions options = new()
        {
            Path = "/sse/broadcast",
            KeepAliveSeconds = 0,
            OperationId = "GetSseBroadcast_Custom",
            Summary = "Custom summary",
            Description = "Custom description",
            Tags = ["SSE", "Custom"],
            ItemSchemaType = typeof(int)
        };
        _ = host.AddSseBroadcast(options);

        d.GenerateDoc();
        var json = d.ToJson(OpenApiSpecVersion.OpenApi3_1);

        Assert.Contains("GetSseBroadcast_Custom", json, StringComparison.Ordinal);
        Assert.Contains("Custom summary", json, StringComparison.Ordinal);
        Assert.Contains("Custom description", json, StringComparison.Ordinal);
        Assert.Contains("\"SSE\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Custom\"", json, StringComparison.Ordinal);
        Assert.Contains("\"integer\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
