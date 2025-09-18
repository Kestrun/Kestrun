using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Claims;
using Kestrun.SharedState;
using Microsoft.AspNetCore.Http;
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
        // Ensure authorization services exist so HasAuthzPolicy can resolve provider
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

    #region ValidateRouteOptions Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithNullApp_ThrowsInvalidOperationException()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        // Don't call EnableConfiguration() so App remains null

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp
        };

        var ex = Assert.Throws<InvalidOperationException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, options, out _));
        
        Assert.Contains("WebApplication is not", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithNullPattern_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = null!,
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp
        };

        var ex = Assert.Throws<ArgumentException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, options, out _));
        
        Assert.Contains("Pattern cannot be null or empty", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithEmptyPattern_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp
        };

        var ex = Assert.Throws<ArgumentException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, options, out _));
        
        Assert.Contains("Pattern cannot be null or empty", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithNullCode_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = null!,
            Language = ScriptLanguage.CSharp
        };

        var ex = Assert.Throws<ArgumentException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, options, out _));
        
        Assert.Contains("ScriptBlock cannot be null or empty", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithEmptyCode_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "",
            Language = ScriptLanguage.CSharp
        };

        var ex = Assert.Throws<ArgumentException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, options, out _));
        
        Assert.Contains("ScriptBlock cannot be null or empty", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithEmptyHttpVerbs_DefaultsToGet()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = []
        };

        var result = KestrunHostMapExtensions.ValidateRouteOptions(host, options, out var routeOptions);
        
        Assert.True(result);
        _ = Assert.Single(routeOptions.HttpVerbs);
        Assert.Equal(HttpVerb.Get, routeOptions.HttpVerbs[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithValidOptions_ReturnsTrue()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-valid",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Post]
        };

        var result = KestrunHostMapExtensions.ValidateRouteOptions(host, options, out var routeOptions);
        
        Assert.True(result);
        Assert.Equal(options.Pattern, routeOptions.Pattern);
        Assert.Equal(options.Code, routeOptions.Code);
        Assert.Equal(options.Language, routeOptions.Language);
        Assert.Equal(options.HttpVerbs, routeOptions.HttpVerbs);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithDuplicateRoute_ThrowOnDuplicate_ThrowsInvalidOperationException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        // Add a route first
        var firstOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-test",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get]
        };
        _ = host.AddMapRoute(firstOptions);

        // Try to add the same route with ThrowOnDuplicate = true
        var duplicateOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-test",
            Code = "Context.Response.StatusCode = 201;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get],
            ThrowOnDuplicate = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() => KestrunHostMapExtensions.ValidateRouteOptions(host, duplicateOptions, out _));
        
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateRouteOptions_WithDuplicateRoute_NoThrow_ReturnsFalse()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        // Add a route first
        var firstOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-no-throw",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get]
        };
        _ = host.AddMapRoute(firstOptions);

        // Try to add the same route with ThrowOnDuplicate = false (default)
        var duplicateOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-no-throw",
            Code = "Context.Response.StatusCode = 201;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get],
            ThrowOnDuplicate = false
        };

        var result = KestrunHostMapExtensions.ValidateRouteOptions(host, duplicateOptions, out _);
        
        Assert.False(result);
    }

    #endregion

    #region CompileScript Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void CompileScript_WithCSharp_ReturnsRequestDelegate()
    {
        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            Arguments = []
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options, logger);
        
        Assert.NotNull(compiled);
        _ = Assert.IsType<RequestDelegate>(compiled);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CompileScript_WithPowerShell_ReturnsRequestDelegate()
    {
        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "$Context.Response.StatusCode = 200",
            Language = ScriptLanguage.PowerShell,
            Arguments = []
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options, logger);
        
        Assert.NotNull(compiled);
        _ = Assert.IsType<RequestDelegate>(compiled);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CompileScript_WithUnsupportedLanguage_ThrowsNotSupportedException()
    {
        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "Context.Response.StatusCode = 200;",
            Language = (ScriptLanguage)999, // Invalid language
            Arguments = []
        };

        var logger = Serilog.Log.Logger;
        
        var ex = Assert.Throws<NotSupportedException>(() => KestrunHostMapExtensions.CompileScript(options, logger));
        
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CompileScript_WithVBNet_ReturnsRequestDelegate()
    {
        var options = new MapRouteOptions
        {
            Pattern = "/test",
            Code = "Context.Response.StatusCode = 200",
            Language = ScriptLanguage.VBNet,
            Arguments = [],
            ExtraImports = [],
            ExtraRefs = []
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options, logger);
        
        Assert.NotNull(compiled);
        _ = Assert.IsType<RequestDelegate>(compiled);
    }

    #endregion

    #region CreateAndRegisterRoute Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void CreateAndRegisterRoute_WithValidInputs_ReturnsEndpointBuilder()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var routeOptions = new MapRouteOptions
        {
            Pattern = "/test-create-register",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get]
        };

        // Create a simple RequestDelegate
        RequestDelegate compiled = context =>
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var result = KestrunHostMapExtensions.CreateAndRegisterRoute(host, routeOptions, compiled);
        
        Assert.NotNull(result);
        Assert.True(host.MapExists("/test-create-register", HttpVerb.Get));
        
        var savedOptions = host.GetMapRouteOptions("/test-create-register", HttpVerb.Get);
        Assert.NotNull(savedOptions);
        Assert.Equal(routeOptions.Pattern, savedOptions!.Pattern);
        Assert.Equal(routeOptions.Language, savedOptions.Language);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CreateAndRegisterRoute_WithMultipleVerbs_RegistersAllMethods()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var routeOptions = new MapRouteOptions
        {
            Pattern = "/test-multi-verbs",
            Code = "Context.Response.StatusCode = 200;",
            Language = ScriptLanguage.CSharp,
            HttpVerbs = [HttpVerb.Get, HttpVerb.Post, HttpVerb.Put]
        };

        RequestDelegate compiled = context =>
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var result = KestrunHostMapExtensions.CreateAndRegisterRoute(host, routeOptions, compiled);
        
        Assert.NotNull(result);
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Get));
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Post));
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Put));
        Assert.False(host.MapExists("/test-multi-verbs", HttpVerb.Delete));
    }

    #endregion
}
