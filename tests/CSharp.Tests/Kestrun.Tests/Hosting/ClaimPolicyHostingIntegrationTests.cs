using System.Security.Claims;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Hosting;

public class ClaimPolicyHostingIntegrationTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_Adds_Policies_Via_PostConfigurer()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["MustBeAdmin"] = new ClaimRule(ClaimTypes.Role, "Admin"),
                ["NamedAlice"] = new ClaimRule(ClaimTypes.Name, "Alice")
            }
        };

        _ = host.AddBasicAuthentication(
            scheme: "BasicX",
            configure: opts =>
            {
                opts.RequireHttps = false; // keep defaults simple
                opts.ClaimPolicyConfig = cfg;
            });

        var app = host.Build();

        Assert.True(host.HasAuthScheme("BasicX"));
        Assert.True(host.HasAuthzPolicy("MustBeAdmin"));
        Assert.True(host.HasAuthzPolicy("NamedAlice"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("MustBeAdmin");
        Assert.NotNull(policy);
        var req = Assert.IsType<ClaimsAuthorizationRequirement>(policy!.Requirements.Single());
        Assert.Equal(ClaimTypes.Role, req.ClaimType);
#pragma warning disable IDE0305
        Assert.Equal(["Admin"], req.AllowedValues!.ToArray());
#pragma warning restore IDE0305
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKeyAuth_Adds_Policies_Via_PostConfigurer()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["AllowUserBob"] = new ClaimRule(ClaimTypes.Name, "Bob")
            }
        };

        _ = host.AddApiKeyAuthentication(
            scheme: "ApiKeyX",
            configure: opts =>
            {
                opts.RequireHttps = false;
                opts.ExpectedKey = "ignored-for-test";
                opts.ClaimPolicyConfig = cfg;
            });

        var app = host.Build();

        Assert.True(host.HasAuthScheme("ApiKeyX"));
        Assert.True(host.HasAuthzPolicy("AllowUserBob"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("AllowUserBob");
        Assert.NotNull(policy);
        var req = Assert.IsType<ClaimsAuthorizationRequirement>(policy!.Requirements.Single());
        Assert.Equal(ClaimTypes.Name, req.ClaimType);
#pragma warning disable IDE0305
        Assert.Equal(["Bob"], req.AllowedValues!.ToArray());
#pragma warning restore IDE0305
    }
}
