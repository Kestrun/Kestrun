using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Mcp;
using Kestrun.OpenApi;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace Kestrun.Tests.Mcp;

[Collection("SharedStateSerial")]
public sealed class KestrunMcpIntegrationTests
{
    [Fact]
    [Trait("Category", "Mcp")]
    public void ListRoutes_ReturnsRegisteredRouteMetadata()
    {
        using var host = CreateHost("TestMcpRouteListing");
        AddHelloRoute(host);
        AddItemRoute(host);

        var inspector = new KestrunRouteInspector();
        var routes = inspector.ListRoutes(host);

        Assert.Equal(2, routes.Count);

        var hello = Assert.Single(routes, static route => route.Pattern == "/hello");
        Assert.Equal(["GET"], hello.Verbs);
        Assert.Equal("Get-Hello", hello.HandlerName);
        Assert.Equal(nameof(ScriptLanguage.Native), hello.HandlerLanguage);

        var items = Assert.Single(routes, static route => route.Pattern == "/items/{id}");
        Assert.Equal(["POST"], items.Verbs);
        Assert.Equal("createItem", items.OperationId);
        Assert.Equal(["items"], items.Tags);
        Assert.Contains("application/json", items.RequestContentTypes);
        Assert.Contains("application/json", items.ResponseContentTypes);
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public void GetRoute_ReturnsOpenApiSchemas_ForOperationId()
    {
        using var host = CreateHost("TestMcpGetRoute");
        AddItemRoute(host);

        var inspector = new KestrunRouteInspector();
        var detail = inspector.GetRoute(host, operationId: "createItem");

        Assert.Null(detail.Error);
        Assert.Equal("/items/{id}", detail.Route.Pattern);
        Assert.True(detail.RequestSchemas.TryGetValue("application/json", out var requestSchema));
        Assert.Equal("object", requestSchema?["type"]?.GetValue<string>());
        Assert.Equal("string", requestSchema?["properties"]?["name"]?["type"]?.GetValue<string>());

        Assert.True(detail.Responses.TryGetValue("200", out var response));
        Assert.Equal("Created", response.Description);
        Assert.Equal("integer", response.Content["application/json"]?["properties"]?["itemId"]?["type"]?.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public void GetRoute_ReturnsStructuredError_WhenRouteMissing()
    {
        using var host = CreateHost("TestMcpMissingRoute");
        AddHelloRoute(host);

        var inspector = new KestrunRouteInspector();
        var detail = inspector.GetRoute(host, pattern: "/missing");

        var error = Assert.IsType<KestrunMcpError>(detail.Error);
        Assert.Equal("route_not_found", error.Code);
        Assert.Equal("/missing", detail.Route.Pattern);
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public void GetOpenApi_ReturnsStructuredJsonDocument()
    {
        using var host = CreateHost("TestMcpOpenApi");
        AddItemRoute(host);

        var provider = new KestrunOpenApiProvider();
        var document = provider.GetOpenApi(host, version: "3.1");

        Assert.Null(document.Error);
        Assert.Equal("3.1.2", document.Version);
        Assert.Equal("3.1.2", document.Document?["openapi"]?.GetValue<string>());
        Assert.NotNull(document.Document?["paths"]?["/items/{id}"]?["post"]);
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public void InspectRuntime_ReturnsSafeConfigurationSnapshot()
    {
        using var host = CreateHost("TestMcpRuntime");
        AddHelloRoute(host);

        var inspector = new KestrunRuntimeInspector();
        var runtime = inspector.Inspect(host);
        var validStatuses = new[] { "configured", "running" };

        Assert.Contains(runtime.Status, validStatuses);
        Assert.Equal(host.ApplicationName, runtime.ApplicationName);
        Assert.Single(runtime.Listeners);
        Assert.Equal(1, runtime.RouteCount);
        Assert.True(runtime.Configuration.ContainsKey("currentUrls"));
        Assert.True(runtime.Configuration.ContainsKey("maxRunspaces"));
    }

    [Theory]
    [Trait("Category", "Mcp")]
    [InlineData("/missing", "POST", null, 404, "route_not_found")]
    [InlineData("/items/42", "POST", "text/plain", 415, "unsupported_media_type")]
    [InlineData("/items/42", "POST", "application/json", 406, "not_acceptable", "text/plain")]
    public void ValidateRequest_ReturnsExpectedOutcomes(
        string path,
        string method,
        string? contentType,
        int expectedStatusCode,
        string expectedErrorCode,
        string? accept = null)
    {
        using var host = CreateHost("TestMcpValidation");
        AddItemRoute(host);

        var validator = new KestrunRequestValidator(new KestrunRouteInspector());
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (contentType is not null)
        {
            headers["Content-Type"] = contentType;
        }

        if (accept is not null)
        {
            headers["Accept"] = accept;
        }

        var result = validator.Validate(
            host,
            new KestrunRequestInput
            {
                Method = method,
                Path = path,
                Headers = headers,
                Body = new { name = "widget" }
            });

        Assert.False(result.IsValid);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(expectedErrorCode, result.Error?.Code);
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public async Task InvokeRoute_ReturnsJsonResponse_ThroughFrameworkPipeline()
    {
        using var host = CreateHost("TestMcpInvoke");
        AddItemRoute(host);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await host.StartAsync(cts.Token);
        try
        {
            var invoker = CreateInvoker(enabled: true, "/items/*");
            var result = await invoker.InvokeAsync(
                host,
                new KestrunRequestInput
                {
                    Method = "POST",
                    Path = "/items/42",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Content-Type"] = "application/json",
                        ["Accept"] = "application/json"
                    },
                    Body = new { name = "widget" }
                },
                cts.Token);

            Assert.Null(result.Error);
            Assert.Equal(200, result.StatusCode);
            Assert.Contains("application/json", result.ContentType, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Body);
            var payload = JsonNode.Parse(result.Body);
            Assert.Equal(42, payload?["itemId"]?.GetValue<int>());
            Assert.Equal("[REDACTED]", result.Headers["Set-Cookie"]);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Mcp")]
    public async Task InvokeRoute_ReturnsStructuredValidationErrors_ForInvalidRequests()
    {
        using var host = CreateHost("TestMcpInvokeValidation");
        AddItemRoute(host);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await host.StartAsync(cts.Token);
        try
        {
            var invoker = CreateInvoker(enabled: true, "/items/*");

            var invalidContentType = await invoker.InvokeAsync(
                host,
                new KestrunRequestInput
                {
                    Method = "POST",
                    Path = "/items/42",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Content-Type"] = "text/plain"
                    },
                    Body = "widget"
                },
                cts.Token);

            Assert.Equal(415, invalidContentType.StatusCode);
            Assert.Equal("unsupported_media_type", invalidContentType.Error?.Code);

            var invalidAccept = await invoker.InvokeAsync(
                host,
                new KestrunRequestInput
                {
                    Method = "POST",
                    Path = "/items/42",
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Content-Type"] = "application/json",
                        ["Accept"] = "text/plain"
                    },
                    Body = new { name = "widget" }
                },
                cts.Token);

            Assert.Equal(406, invalidAccept.StatusCode);
            Assert.Equal("not_acceptable", invalidAccept.Error?.Code);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    private static KestrunHost CreateHost(string name)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost(name, logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        host.EnableConfiguration();
        return host;
    }

    private static void AddHelloRoute(KestrunHost host)
    {
        var options = new MapRouteOptions
        {
            Pattern = "/hello",
            HttpVerbs = [HttpVerb.Get],
            HandlerName = "Get-Hello",
            DefaultResponseContentType = new Dictionary<string, ICollection<ContentTypeWithSchema>>(StringComparer.OrdinalIgnoreCase)
            {
                ["200"] = [new ContentTypeWithSchema("application/json")]
            }
        };

        _ = host.AddMapRoute(options, async context =>
        {
            await context.Response.WriteResponseAsync(new { message = "hello" }, StatusCodes.Status200OK);
        }, out _);
    }

    private static void AddItemRoute(KestrunHost host)
    {
        var options = new MapRouteOptions
        {
            Pattern = "/items/{id}",
            HttpVerbs = [HttpVerb.Post],
            ScriptCode = new LanguageOptions { Language = ScriptLanguage.Native },
            HandlerName = "Invoke-CreateItem",
            AllowedRequestContentTypes = ["application/json"],
            DefaultResponseContentType = new Dictionary<string, ICollection<ContentTypeWithSchema>>(StringComparer.OrdinalIgnoreCase)
            {
                ["200"] = [new ContentTypeWithSchema("application/json")]
            },
            IsOpenApiAnnotatedFunctionRoute = true
        };

        options.OpenAPI[HttpVerb.Post] = new OpenAPIPathMetadata(options)
        {
            Pattern = options.Pattern,
            Summary = "Create item",
            Description = "Creates an item and echoes the identifier.",
            OperationId = "createItem",
            Tags = ["items"],
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = CreateRequestSchema()
                    }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "Created",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = CreateResponseSchema()
                        }
                    }
                }
            }
        };

        _ = host.AddMapRoute(options, async context =>
        {
            var routeId = context.HttpContext.Request.RouteValues["id"]?.ToString();
            context.Response.Headers["Set-Cookie"] = "session=super-secret";
            await context.Response.WriteResponseAsync(
                new
                {
                    itemId = int.Parse(routeId ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    name = "widget"
                },
                StatusCodes.Status200OK);
        }, out _);
    }

    private static IKestrunRequestInvoker CreateInvoker(bool enabled, params string[] allowedPaths)
    {
        var validator = new KestrunRequestValidator(new KestrunRouteInspector());
        return new KestrunRequestInvoker(
            validator,
            new KestrunRequestInvokerOptions
            {
                EnableInvocation = enabled,
                AllowedPathPatterns = allowedPaths
            });
    }

    private static OpenApiSchema CreateRequestSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new SortedSet<string>(StringComparer.Ordinal) { "name" },
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
    }

    private static OpenApiSchema CreateResponseSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new SortedSet<string>(StringComparer.Ordinal) { "itemId", "name" },
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["itemId"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };
    }
}
