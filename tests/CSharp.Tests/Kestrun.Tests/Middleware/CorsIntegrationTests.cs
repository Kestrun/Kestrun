using Kestrun.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Kestrun.Hosting;
using Xunit;
using Kestrun.Utilities;

namespace KestrunTests.Middleware;

public class CorsIntegrationTests
{
    [Fact]
    public async Task AddCorsPolicy_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Act - Initially false
        Assert.False(host.CorsPolicyDefined);

        // Add a CORS policy
        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader();
        _ = host.AddCorsDefaultPolicy(builder);

        // Assert - Should be true after adding policy
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public async Task AddCorsPolicy_WithNamedPolicy_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Act
        Assert.False(host.CorsPolicyDefined);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod();
        _ = host.AddCorsPolicy("MyPolicy", builder);

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public async Task AddCorsDefaultPolicyAllowAll_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Act
        Assert.False(host.CorsPolicyDefined);

        _ = host.AddCorsDefaultPolicyAllowAll();

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public async Task AddCorsPolicyAllowAll_WithName_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Act
        _ = host.AddCorsPolicyAllowAll("AllowAllPolicy");

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public async Task MultiplePolicies_AllSetFlag()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Act - Add multiple policies
        _ = host.AddCorsPolicy("Policy1", builder => builder.WithOrigins("http://localhost:3000"));
        _ = host.AddCorsPolicy("Policy2", builder => builder.WithOrigins("http://localhost:4000"));
        _ = host.AddCorsPolicy("Policy3", builder => builder.WithOrigins("http://localhost:5000"));

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public async Task CorsPolicy_WithMultipleOrigins_ReturnsMatchingOrigin()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000", "http://localhost:4000")
            .AllowAnyMethod()
            .AllowAnyHeader();
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/test", HttpVerb.Get, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { message = "ok" });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act - Test first origin
        var client = testServer.CreateClient();
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/test");
        request1.Headers.Add("Origin", "http://localhost:3000");
        var response1 = await client.SendAsync(request1);

        // Assert
        Assert.True(response1.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:3000", response1.Headers.GetValues("Access-Control-Allow-Origin").First());

        // Act - Test second origin
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/test");
        request2.Headers.Add("Origin", "http://localhost:4000");
        var response2 = await client.SendAsync(request2);

        // Assert
        Assert.Equal("http://localhost:4000", response2.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task CorsPolicy_BlocksNonAllowedOrigin()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader();
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/test", HttpVerb.Get, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { message = "ok" });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        request.Headers.Add("Origin", "http://evil.com");
        var response = await client.SendAsync(request);

        // Assert - CORS headers should not be present for blocked origin
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CorsPolicy_WithCredentials_IncludesCredentialsHeader()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/secure", HttpVerb.Get, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { authenticated = true });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Add("Origin", "http://localhost:3000");
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").First());
    }

    [Fact]
    public async Task CorsPolicy_WithExposedHeaders_IncludesExposeHeadersHeader()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Total-Count", "X-Page-Number");
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/data", HttpVerb.Get, ctx =>
        {
            ctx.Response.Headers.Append("X-Total-Count", "42");
            ctx.Response.Headers.Append("X-Page-Number", "1");
            ctx.Response.WriteAsJsonAsync(new { data = "test" });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/data");
        request.Headers.Add("Origin", "http://localhost:3000");
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Expose-Headers"));
        var exposedHeaders = response.Headers.GetValues("Access-Control-Expose-Headers").First();
        Assert.Contains("X-Total-Count", exposedHeaders);
        Assert.Contains("X-Page-Number", exposedHeaders);
    }

    [Fact]
    public async Task CorsPolicy_PreflightWithMaxAge_IncludesMaxAgeHeader()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .WithMethods("POST", "PUT", "DELETE")
            .AllowAnyHeader()
            .SetPreflightMaxAge(TimeSpan.FromHours(2));
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/update", HttpVerb.Post, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { updated = true });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act - Preflight request
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/update");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Max-Age"));
        Assert.Equal("7200", response.Headers.GetValues("Access-Control-Max-Age").First());
    }

    [Fact]
    public async Task CorsPolicy_SpecificMethodsOnly_RejectsOtherMethods()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .WithMethods("GET", "POST")
            .AllowAnyHeader();
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/resource", HttpVerb.Get | HttpVerb.Post | HttpVerb.Delete, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { method = ctx.Request.Method });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act - Preflight for DELETE (not allowed by policy)
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/resource");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "DELETE");
        var response = await client.SendAsync(request);

        // Assert - DELETE should not be in allowed methods
        if (response.Headers.Contains("Access-Control-Allow-Methods"))
        {
            var allowedMethods = response.Headers.GetValues("Access-Control-Allow-Methods").First();
            Assert.DoesNotContain("DELETE", allowedMethods);
        }
    }

    [Fact]
    public async Task CorsPolicy_AllowAll_ReturnsWildcard()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        _ = host.AddCorsDefaultPolicyAllowAll();

        host.AddMapRoute("/public", HttpVerb.Get, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { access = "public" });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/public");
        request.Headers.Add("Origin", "http://anywhere.com");
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task NamedCorsPolicy_AppliesToSpecificRoute()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        // Add named policy
        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod();
        _ = host.AddCorsPolicy("RestrictedPolicy", builder);

        // Route with named policy
        host.AddMapRoute("/restricted", HttpVerb.Get,
            ctx =>
            {
                ctx.Response.WriteAsJsonAsync(new { restricted = true });
                return Task.CompletedTask;
            },
            corsPolicyName: "RestrictedPolicy");

        // Route without policy
        host.AddMapRoute("/open", HttpVerb.Get, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { open = true });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act - Test restricted route
        var client = testServer.CreateClient();
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/restricted");
        request1.Headers.Add("Origin", "http://localhost:3000");
        var response1 = await client.SendAsync(request1);

        // Assert - Restricted route has CORS
        Assert.True(response1.Headers.Contains("Access-Control-Allow-Origin"));

        // Act - Test open route (no CORS policy)
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/open");
        request2.Headers.Add("Origin", "http://localhost:3000");
        var response2 = await client.SendAsync(request2);

        // Assert - Open route has no CORS headers (no default policy set)
        Assert.False(response2.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CorsPolicy_SpecificHeaders_AllowsOnlyThoseHeaders()
    {
        // Arrange
        var host = new KestrunHost("TestServer");
        host.AddEndpoint(5000);

        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .WithHeaders("Content-Type", "Authorization");
        _ = host.AddCorsDefaultPolicy(builder);

        host.AddMapRoute("/api", HttpVerb.Post, ctx =>
        {
            ctx.Response.WriteAsJsonAsync(new { received = true });
            return Task.CompletedTask;
        });

        host.EnableConfiguration();
        var testServer = new TestServer(host.GetWebApplicationBuilder());

        // Act - Preflight request
        var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type,Authorization");
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").First();
        Assert.Contains("Content-Type", allowedHeaders);
        Assert.Contains("Authorization", allowedHeaders);
    }
}
