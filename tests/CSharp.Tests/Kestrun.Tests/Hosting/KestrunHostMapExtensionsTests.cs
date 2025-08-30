using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Claims;
using Kestrun.SharedState;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Kestrun.Utilities;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostMapExtensionsTests
{
    private static void SanitizeSharedGlobals()
    {
        foreach (var key in SharedStateStore.KeySnapshot())
        {
            _ = SharedStateStore.Set(key, null);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_Code_DefaultsToGet_WhenNoVerbsSpecified()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/t-code-default",
            HttpVerbs = [],
            Code = "Context.Response.StatusCode = 204;",
            Language = ScriptLanguage.CSharp
        };

        var map = host.AddMapRoute(options);
        Assert.NotNull(map);

        Assert.True(host.MapExists("/t-code-default", HttpVerb.Get));
        var saved = host.GetMapRouteOptions("/t-code-default", HttpVerb.Get);
        Assert.NotNull(saved);
        Assert.Equal(ScriptLanguage.CSharp, saved!.Language);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_Duplicate_WithThrowOnDuplicate_Throws()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/dup",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            ThrowOnDuplicate = true
        };

        Assert.NotNull(host.AddMapRoute(options));
        _ = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_Duplicate_WithoutThrow_ReturnsNull()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/dup2",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            ThrowOnDuplicate = false
        };

        var first = host.AddMapRoute(options);
        var second = host.AddMapRoute(options);
        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MapExists_MultiVerb_Works()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/multi",
            HttpVerbs = [HttpVerb.Get, HttpVerb.Post],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp
        };

        Assert.NotNull(host.AddMapRoute(options));
#pragma warning disable IDE0300
        Assert.True(host.MapExists("/multi", new[] { HttpVerb.Get }));
        Assert.True(host.MapExists("/multi", new[] { HttpVerb.Post }));
        Assert.True(host.MapExists("/multi", new[] { HttpVerb.Get, HttpVerb.Post }));
        Assert.False(host.MapExists("/multi", new[] { HttpVerb.Put }));
#pragma warning restore IDE0300
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_RequireSchemes_Unregistered_Throws()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        // Ensure auth services exist so HasAuthScheme can resolve provider
        _ = host.AddBasicAuthentication("InitAuth", _ => { });
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/auth-needed",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            RequireSchemes = ["NotRegisteredScheme"]
        };

        _ = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_RequireSchemes_Registered_Ok()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        // Register a basic auth scheme
        _ = host.AddBasicAuthentication("BasicX", _ => { });
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/auth-ok",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            RequireSchemes = ["BasicX"]
        };

        var map = host.AddMapRoute(options);
        Assert.NotNull(map);
        Assert.True(host.MapExists("/auth-ok", HttpVerb.Get));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_RequirePolicies_Unregistered_Throws()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        // Ensure authorization services exist so HasAuthPolicy can resolve provider
        _ = host.AddAuthorization();
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/policy-needed",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            RequirePolicies = ["NonExistingPolicy"]
        };

        _ = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_RequirePolicies_Registered_Ok()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        // Register a scheme with a claim policy
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('x', 64)));
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["MustBeAlice"] = new ClaimRule(System.Security.Claims.ClaimTypes.Name, "Alice")
            }
        };

        _ = host.AddJwtBearerAuthentication("BearerX", tvp, claimPolicy: cfg);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/policy-ok",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            RequirePolicies = ["MustBeAlice"]
        };

        var map = host.AddMapRoute(options);
        Assert.NotNull(map);
        Assert.True(host.MapExists("/policy-ok", HttpVerb.Get));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHtmlTemplateRoute_MapsGet_WhenFileExists()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "<html><body>Hello</body></html>");

        try
        {
            var map = host.AddHtmlTemplateRoute(new MapRouteOptions
            {
                Pattern = "/tmpl-ok",
                HttpVerbs = [HttpVerb.Get]
            }, tmp);

            Assert.NotNull(map);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticOverride_Code_RegistersMapping_AfterBuild()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        _ = host.AddStaticMapOverride(
            pattern: "/override",
            code: "Context.Response.StatusCode = 201;",
            language: ScriptLanguage.CSharp);

        // Route is queued and applied during Build
        _ = host.Build();

        Assert.True(host.MapExists("/override", HttpVerb.Get));
        var opts = host.GetMapRouteOptions("/override", HttpVerb.Get);
        Assert.NotNull(opts);
        Assert.Equal(ScriptLanguage.CSharp, opts!.Language);
    }
}
