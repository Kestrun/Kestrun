using Kestrun.Hosting.Options;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class MapRouteOptionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesCollections()
    {
        var options = new MapRouteOptions();

        Assert.NotNull(options.HttpVerbs);
        Assert.Empty(options.HttpVerbs);
        Assert.NotNull(options.RequireSchemes);
        Assert.Empty(options.RequireSchemes);
        Assert.NotNull(options.RequirePolicies);
        Assert.Empty(options.RequirePolicies);
        Assert.NotNull(options.Endpoints);
        Assert.Empty(options.Endpoints);
        Assert.NotNull(options.OpenAPI);
        Assert.NotNull(options.ScriptCode);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Pattern_CanBeSet()
    {
        var options = new MapRouteOptions { Pattern = "/api/users" };

        Assert.Equal("/api/users", options.Pattern);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void HttpVerbs_CanBeSet()
    {
        var options = new MapRouteOptions
        {
            HttpVerbs = [HttpVerb.Get, HttpVerb.Post]
        };

        Assert.Equal(2, options.HttpVerbs.Count);
        Assert.Contains(HttpVerb.Get, options.HttpVerbs);
        Assert.Contains(HttpVerb.Post, options.HttpVerbs);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RequireSchemes_CanBeSet()
    {
        var options = new MapRouteOptions
        {
            RequireSchemes = ["Bearer", "ApiKey"]
        };

        Assert.Equal(2, options.RequireSchemes.Count());
        Assert.Contains("Bearer", options.RequireSchemes);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RequirePolicies_CanBeSet()
    {
        var options = new MapRouteOptions
        {
            RequirePolicies = ["Admin", "User"]
        };

        Assert.NotNull(options.RequirePolicies);
        Assert.Equal(2, options.RequirePolicies.Count());
        Assert.Contains("Admin", options.RequirePolicies);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CorsPolicy_CanBeSet()
    {
        var options = new MapRouteOptions { CorsPolicy = "AllowAll" };

        Assert.Equal("AllowAll", options.CorsPolicy);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ShortCircuit_DefaultsToFalse()
    {
        var options = new MapRouteOptions();

        Assert.False(options.ShortCircuit);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ShortCircuit_CanBeSet()
    {
        var options = new MapRouteOptions { ShortCircuit = true };

        Assert.True(options.ShortCircuit);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ShortCircuitStatusCode_DefaultsToNull()
    {
        var options = new MapRouteOptions();

        Assert.Null(options.ShortCircuitStatusCode);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ShortCircuitStatusCode_CanBeSet()
    {
        var options = new MapRouteOptions { ShortCircuitStatusCode = 204 };

        Assert.Equal(204, options.ShortCircuitStatusCode);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AllowAnonymous_DefaultsToFalse()
    {
        var options = new MapRouteOptions();

        Assert.False(options.AllowAnonymous);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DisableAntiforgery_DefaultsToFalse()
    {
        var options = new MapRouteOptions();

        Assert.False(options.DisableAntiforgery);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DisableResponseCompression_DefaultsToFalse()
    {
        var options = new MapRouteOptions();

        Assert.False(options.DisableResponseCompression);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RateLimitPolicyName_CanBeSet()
    {
        var options = new MapRouteOptions { RateLimitPolicyName = "Standard" };

        Assert.Equal("Standard", options.RateLimitPolicyName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ThrowOnDuplicate_DefaultsToFalse()
    {
        var options = new MapRouteOptions();

        Assert.False(options.ThrowOnDuplicate);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Pattern_RoundTrip_Works()
    {
        var options = new MapRouteOptions { Pattern = "/api/test" };

        Assert.Equal("/api/test", options.Pattern);
    }
}
