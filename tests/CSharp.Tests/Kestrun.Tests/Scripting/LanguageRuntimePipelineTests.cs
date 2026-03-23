using Kestrun.Scripting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Kestrun.Tests.Scripting;

[Trait("Category", "Scripting")]
public sealed class LanguageRuntimePipelineTests
{
    [Fact]
    public async Task UseLanguageRuntime_AppliesBranchOnlyWhenMetadataMatches()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        var app = builder.Build();

        _ = app.UseLanguageRuntime(ScriptLanguage.PowerShell, branch =>
        {
            _ = branch.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Lang"] = "PowerShell";
                await next(ctx);
            });
        });

        _ = app.MapGet("/ps", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.PowerShell);

        _ = app.MapGet("/cs", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.CSharp);

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();

        var ps = await client.GetAsync("/ps", TestContext.Current.CancellationToken);
        Assert.True(ps.Headers.Contains("X-Lang"));

        var cs = await client.GetAsync("/cs", TestContext.Current.CancellationToken);
        Assert.False(cs.Headers.Contains("X-Lang"));

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WithLanguage_AddsScriptLanguageAttributeMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        var app = builder.Build();

        _ = app.MapGet("/ps", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.PowerShell);

        await app.StartAsync(TestContext.Current.CancellationToken);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();
        var endpoint = endpoints.FirstOrDefault(e => string.Equals(e.RoutePattern.RawText, "/ps", StringComparison.Ordinal));

        Assert.NotNull(endpoint);

        var attr = endpoint.Metadata.GetMetadata<ScriptLanguageAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(ScriptLanguage.PowerShell, attr.Language);

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UseLanguageRuntime_WhenNoEndpointMetadata_DoesNotApplyBranch()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        var app = builder.Build();

        _ = app.UseLanguageRuntime(ScriptLanguage.PowerShell, branch =>
        {
            _ = branch.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Lang"] = "PowerShell";
                await next(ctx);
            });
        });

        // No WithLanguage metadata
        _ = app.MapGet("/plain", () => Results.Text("ok"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = app.GetTestClient();

        var resp = await client.GetAsync("/plain", TestContext.Current.CancellationToken);
        Assert.False(resp.Headers.Contains("X-Lang"));

        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}

