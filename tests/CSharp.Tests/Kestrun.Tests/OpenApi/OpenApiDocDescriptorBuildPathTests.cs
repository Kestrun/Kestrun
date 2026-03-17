using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorBuildPathTests
{
    [Fact]
    public void BuildPathsFromRegisteredRoutes_IgnoresNullOrEmptyInput()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = InvokePrivate(descriptor, "BuildPathsFromRegisteredRoutes", [null]);
        Assert.True(descriptor.Document.Paths == null || descriptor.Document.Paths.Count == 0);

        _ = InvokePrivate(descriptor, "BuildPathsFromRegisteredRoutes", [new Dictionary<(string Pattern, HttpVerb Method), MapRouteOptions>()]);
        Assert.True(descriptor.Document.Paths == null || descriptor.Document.Paths.Count == 0);
    }

    [Fact]
    public void BuildPathsFromRegisteredRoutes_BuildsOperations_AndAppliesPathLevelMetadata()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var map = new MapRouteOptions { Pattern = "/fallback" };
        var metaGet = new OpenAPIPathMetadata(map)
        {
            Pattern = "/orders/{id}",
            Summary = "Get order",
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "ok" } }
        };
        var metaPost = new OpenAPIPathMetadata(map)
        {
            Pattern = "/orders/{id}",
            Summary = "Create order",
            Responses = new OpenApiResponses { ["201"] = new OpenApiResponse { Description = "created" } }
        };

        map.OpenAPI[HttpVerb.Get] = metaGet;
        map.OpenAPI[HttpVerb.Post] = metaPost;
        map.PathLevelOpenAPIMetadata = new OpenAPICommonMetadata(map)
        {
            Summary = "Orders",
            Description = "Order endpoints",
            Servers = [new OpenApiServer { Url = "https://api.example.test" }],
            Parameters = [new OpenApiParameter { Name = "tenant", In = ParameterLocation.Header, Required = false }]
        };

        var routes = new Dictionary<(string Pattern, HttpVerb Method), MapRouteOptions>
        {
            [("/fallback", HttpVerb.Get)] = map
        };

        _ = InvokePrivate(descriptor, "BuildPathsFromRegisteredRoutes", [routes]);

        Assert.NotNull(descriptor.Document.Paths);
        Assert.True(descriptor.Document.Paths.ContainsKey("/orders/{id}"));

        var path = Assert.IsType<OpenApiPathItem>(descriptor.Document.Paths["/orders/{id}"]);
        Assert.Equal("Orders", path.Summary);
        Assert.Equal("Order endpoints", path.Description);
        Assert.NotNull(path.Operations);
        Assert.Contains(HttpMethod.Get, path.Operations.Keys);
        Assert.Contains(HttpMethod.Post, path.Operations.Keys);
    }

    [Fact]
    public void BuildPathsFromRegisteredRoutes_SkipsDisabledOrMismatchedDocumentEntries()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var map = new MapRouteOptions { Pattern = "/ignored" };

        var mismatched = new OpenAPIPathMetadata(map)
        {
            Pattern = "/inventory",
            DocumentId = ["OtherDoc"],
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "ok" } }
        };

        var disabled = new OpenAPIPathMetadata(map)
        {
            Pattern = "/inventory",
            Enabled = false,
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "ok" } }
        };

        map.OpenAPI[HttpVerb.Get] = mismatched;
        map.OpenAPI[HttpVerb.Post] = disabled;

        var routes = new Dictionary<(string Pattern, HttpVerb Method), MapRouteOptions>
        {
            [("/ignored", HttpVerb.Get)] = map
        };

        _ = InvokePrivate(descriptor, "BuildPathsFromRegisteredRoutes", [routes]);

        Assert.NotNull(descriptor.Document.Paths);
        Assert.True(descriptor.Document.Paths.ContainsKey("/inventory"));

        var path = Assert.IsType<OpenApiPathItem>(descriptor.Document.Paths["/inventory"]);
        Assert.True(path.Operations == null || path.Operations.Count == 0);
    }

    private static object? InvokePrivate(object target, string methodName, object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(target, args);
    }
}
