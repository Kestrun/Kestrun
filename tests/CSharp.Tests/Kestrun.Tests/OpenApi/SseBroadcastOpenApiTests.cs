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

        _ = host.AddSseBroadcast(path: "/sse/broadcast", keepAliveSeconds: 0);

        Assert.True(host.RegisteredRoutes.ContainsKey(("/sse/broadcast", HttpVerb.Get)));
        Assert.True(host.RegisteredRoutes[("/sse/broadcast", HttpVerb.Get)].OpenAPI.ContainsKey(HttpVerb.Get));

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

        _ = host.AddSseBroadcast(
            path: "/sse/broadcast",
            keepAliveSeconds: 0,
            openApi: new Kestrun.Hosting.Options.SseBroadcastOpenApiOptions
            {
                OperationId = "GetSseBroadcast_Custom",
                Summary = "Custom summary",
                Description = "Custom description",
                Tags = ["SSE", "Custom"],
                ItemSchemaType = typeof(int)
            });

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
