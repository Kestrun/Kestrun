using System.Security.Claims;
using Kestrun.Authentication;
using Kestrun.Claims;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiHelperTests
{
    [Fact]
    public void AddSecurityRequirementObject_NullHost_ThrowsArgumentNullException()
    {
        var security = new List<Dictionary<string, List<string>>>();

        _ = Assert.Throws<ArgumentNullException>(() => OpenApiHelper.AddSecurityRequirementObject(null!, "Basic", [], security));
    }

    [Fact]
    public void AddSecurityRequirementObject_NullPolicyList_ThrowsArgumentNullException()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var security = new List<Dictionary<string, List<string>>>();

        _ = Assert.Throws<ArgumentNullException>(() => host.AddSecurityRequirementObject("Basic", null!, security));
    }

    [Fact]
    public void AddSecurityRequirementObject_NullSecuritySchemes_ThrowsArgumentNullException()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        _ = Assert.Throws<ArgumentNullException>(() => host.AddSecurityRequirementObject("Basic", [], null!));
    }

    [Fact]
    public void AddSecurityRequirementObject_WithExplicitSchemeOnly_AddsEmptyScopeListAndReturnsScheme()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var security = new List<Dictionary<string, List<string>>>();

        var schemes = host.AddSecurityRequirementObject("Basic", [], security);

        _ = Assert.Single(schemes);
        Assert.Equal("Basic", schemes[0]);
        _ = Assert.Single(security);
        Assert.True(security[0].ContainsKey("Basic"));
        Assert.Empty(security[0]["Basic"]);
    }

    [Fact]
    public void AddSecurityRequirementObject_WithPolicies_MapsPoliciesToRegisteredSchemes_AndDeduplicates()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        var cfg = new ClaimPolicyConfig
        {
            Policies = new Dictionary<string, ClaimRule>
            {
                ["CanRead"] = new ClaimRule(ClaimTypes.Role, "Allows read", "Reader"),
                ["CanWrite"] = new ClaimRule(ClaimTypes.Role, "Allows write", "Writer")
            }
        };

        host.RegisteredAuthentications.Upsert<BasicAuthenticationOptions>(
            "BasicAuth",
            AuthenticationType.Basic,
            options => options.ClaimPolicyConfig = cfg);

        var security = new List<Dictionary<string, List<string>>>();

        var schemes = host.AddSecurityRequirementObject(
            scheme: "BasicAuth",
            policyList: ["CanRead", "CanRead", "CanWrite", "UnknownPolicy"],
            securitySchemes: security);

        Assert.Contains("BasicAuth", schemes);
        _ = Assert.Single(security);
        Assert.True(security[0].TryGetValue("BasicAuth", out var scopes));
        Assert.NotNull(scopes);
        Assert.Equal(2, scopes.Count);
        Assert.Contains("CanRead", scopes);
        Assert.Contains("CanWrite", scopes);
    }

    [Fact]
    public void AddSecurityRequirementObject_WithWhitespaceExplicitScheme_IgnoresExplicitScheme()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var security = new List<Dictionary<string, List<string>>>();

        var schemes = host.AddSecurityRequirementObject("   ", [], security);

        Assert.Empty(schemes);
        _ = Assert.Single(security);
        Assert.Empty(security[0]);
    }
}
