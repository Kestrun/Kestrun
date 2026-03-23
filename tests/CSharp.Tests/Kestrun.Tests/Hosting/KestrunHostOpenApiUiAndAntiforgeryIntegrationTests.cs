using System.Net;
using System.Text.Json;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Xunit;

namespace Kestrun.Tests.Hosting;

[Collection("SharedStateSerial")]
public sealed class KestrunHostOpenApiUiAndAntiforgeryIntegrationTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public async Task OpenApiMapRoute_AndUiRoutes_ServeExpectedPayloads()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("TestOpenApiUi", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        EnsureUserExExample(host);
        host.EnableConfiguration();

        _ = host.AddOpenApiMapRoute(new OpenApiMapRouteOptions(new MapRouteOptions()));
        _ = host.AddSwaggerUiRoute(new MapRouteOptions(), new Uri("/openapi/v3.0/openapi.json", UriKind.Relative));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await host.StartAsync(cts.Token);
        try
        {
            using var client = CreateClient(host);

            var openApiJson = await client.GetAsync("openapi/v3.0/openapi.json", cts.Token);
            Assert.True(openApiJson.IsSuccessStatusCode);
            Assert.Equal("application/json", openApiJson.Content.Headers.ContentType?.MediaType);

            var openApiYaml = await client.GetAsync("openapi/v3.0/openapi.yaml", cts.Token);
            Assert.True(openApiYaml.IsSuccessStatusCode);
            Assert.Equal("application/yaml", openApiYaml.Content.Headers.ContentType?.MediaType);

            var invalidFormat = await client.GetAsync("openapi/v3.0/openapi.txt", cts.Token);
            Assert.Equal(HttpStatusCode.NotFound, invalidFormat.StatusCode);

            var swaggerUi = await client.GetAsync("docs/swagger", cts.Token);
            Assert.True(swaggerUi.IsSuccessStatusCode);
            var html = await swaggerUi.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("openapi", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task AddAntiforgeryTokenRoute_ReturnsJsonTokenPayload()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("TestAntiforgery", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        _ = host.AddAntiforgery();
        host.EnableConfiguration();
        _ = host.AddAntiforgeryTokenRoute("/csrf-token");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await host.StartAsync(cts.Token);
        try
        {
            using var client = CreateClient(host);
            var response = await client.GetAsync("csrf-token", cts.Token);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(payload);
            Assert.True(doc.RootElement.TryGetProperty("token", out var tokenElement));
            Assert.False(string.IsNullOrWhiteSpace(tokenElement.GetString()));
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddAntiforgeryTokenRoute_WhenConfigurationNotEnabled_Throws()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("TestAntiforgeryNoConfig", logger, AppContext.BaseDirectory);

        var exception = Assert.Throws<InvalidOperationException>(() => host.AddAntiforgeryTokenRoute());
        Assert.Contains("Build()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddSwaggerUiRoute_WithNonGetVerb_ThrowsArgumentException()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var host = new KestrunHost("TestSwaggerVerbValidation", logger, AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            HttpVerbs = [HttpVerb.Post]
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            host.AddSwaggerUiRoute(options, new Uri("http://localhost/openapi/v3.0/openapi.json")));

        Assert.Contains("only support GET", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(KestrunHost host)
    {
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(appField);
        var app = Assert.IsType<WebApplication>(appField.GetValue(host));

        var listenerUri = app.Urls.Select(static url => new Uri(url)).First();
        var baseAddress = new UriBuilder(listenerUri)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;

        return new HttpClient
        {
            BaseAddress = baseAddress
        };
    }

    private static void EnsureUserExExample(KestrunHost host)
    {
        var descriptor = host.GetOrCreateOpenApiDocument(OpenApiDocDescriptor.DefaultDocumentationId);
        descriptor.AddComponentExample(
            "UserEx",
            new Microsoft.OpenApi.OpenApiExample
            {
                Summary = "User example",
                Description = "Example used by xUnit tests",
                Value = OpenApiJsonNodeFactory.ToNode(new { Name = "Alice", Age = 42 })
            });
    }
}
