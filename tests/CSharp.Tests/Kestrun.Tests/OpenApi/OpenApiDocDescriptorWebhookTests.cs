using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiDocDescriptorWebhookTests
{
    [Fact]
    public void BuildWebhooks_IgnoresNullAndEmptyInputs()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        _ = InvokePrivate(descriptor, "BuildWebhooks", [null]);
        Assert.Null(descriptor.Document.Webhooks);

        _ = InvokePrivate(descriptor, "BuildWebhooks", [new Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>()]);
        Assert.Null(descriptor.Document.Webhooks);
    }

    [Fact]
    public void BuildWebhooks_GroupsByPattern_AndFiltersByDocumentId()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var included = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            DocumentId = [OpenApiDocDescriptor.DefaultDocumentationId],
            Summary = "included",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "ok" }
            }
        };

        var excluded = new OpenAPIPathMetadata(new MapRouteOptions())
        {
            DocumentId = ["OtherDoc"],
            Summary = "excluded",
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "ok" }
            }
        };

        var metadata = new Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>
        {
            [("/events/order-created", HttpVerb.Post)] = included,
            [("/events/order-created", HttpVerb.Put)] = excluded,
            [(" ", HttpVerb.Post)] = included
        };

        _ = InvokePrivate(descriptor, "BuildWebhooks", [metadata]);

        Assert.NotNull(descriptor.Document.Webhooks);
        Assert.True(descriptor.Document.Webhooks.ContainsKey("/events/order-created"));
        Assert.False(descriptor.Document.Webhooks.ContainsKey(" "));

        var item = Assert.IsType<OpenApiPathItem>(descriptor.Document.Webhooks["/events/order-created"]);
        Assert.NotNull(item.Operations);
        _ = Assert.Single(item.Operations);
        Assert.Contains(HttpMethod.Post, item.Operations.Keys);
    }

    private static object? InvokePrivate(object target, string methodName, object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(target, args);
    }
}
