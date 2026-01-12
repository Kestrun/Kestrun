using Kestrun.Scripting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace KestrunTests.Scripting;

[Trait("Category", "Scripting")]
public sealed class LanguageRuntimePipelineTests
{
    [Fact]
    public async Task UseLanguageRuntime_AppliesBranchOnlyWhenMetadataMatches()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.UseLanguageRuntime(ScriptLanguage.PowerShell, branch =>
        {
            branch.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Lang"] = "PowerShell";
                await next(ctx);
            });
        });

        app.MapGet("/ps", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.PowerShell);

        app.MapGet("/cs", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.CSharp);

        await app.StartAsync();
        var client = app.GetTestClient();

        var ps = await client.GetAsync("/ps");
        Assert.True(ps.Headers.Contains("X-Lang"));

        var cs = await client.GetAsync("/cs");
        Assert.False(cs.Headers.Contains("X-Lang"));

        await app.StopAsync();
    }

    [Fact]
    public async Task WithLanguage_AddsScriptLanguageAttributeMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.MapGet("/ps", () => Results.Text("ok"))
            .WithLanguage(ScriptLanguage.PowerShell);

        await app.StartAsync();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();
        var endpoint = endpoints.FirstOrDefault(e => string.Equals(e.RoutePattern.RawText, "/ps", StringComparison.Ordinal));

        Assert.NotNull(endpoint);

        var attr = endpoint!.Metadata.GetMetadata<ScriptLanguageAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(ScriptLanguage.PowerShell, attr!.Language);

        await app.StopAsync();
    }

    [Fact]
    public async Task UseLanguageRuntime_WhenNoEndpointMetadata_DoesNotApplyBranch()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.UseLanguageRuntime(ScriptLanguage.PowerShell, branch =>
        {
            branch.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Lang"] = "PowerShell";
                await next(ctx);
            });
        });

        // No WithLanguage metadata
        app.MapGet("/plain", () => Results.Text("ok"));

        await app.StartAsync();
        var client = app.GetTestClient();

        var resp = await client.GetAsync("/plain");
        Assert.False(resp.Headers.Contains("X-Lang"));

        await app.StopAsync();
    }
}
