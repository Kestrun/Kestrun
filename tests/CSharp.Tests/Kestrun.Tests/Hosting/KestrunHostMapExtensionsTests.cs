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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 204;",
                Language = ScriptLanguage.CSharp
            }
        };

        var map = host.AddMapRoute(options);
        Assert.NotNull(map);

        Assert.True(host.MapExists("/t-code-default", HttpVerb.Get));
        var saved = host.GetMapRouteOptions("/t-code-default", HttpVerb.Get);
        Assert.NotNull(saved);
        Assert.Equal(ScriptLanguage.CSharp, saved!.ScriptCode.Language);
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
        Assert.Equal(ScriptLanguage.CSharp, opts!.ScriptCode.Language);
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = null!,
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = "",
                Language = ScriptLanguage.CSharp
            }
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            HttpVerbs = [HttpVerb.Post]
        };

        var result = KestrunHostMapExtensions.ValidateRouteOptions(host, options, out var routeOptions);

        Assert.True(result);
        Assert.Equal(options.Pattern, routeOptions.Pattern);
        Assert.Equal(options.ScriptCode.Code, routeOptions.ScriptCode.Code);
        Assert.Equal(options.ScriptCode.Language, routeOptions.ScriptCode.Language);
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            HttpVerbs = [HttpVerb.Get]
        };
        _ = host.AddMapRoute(firstOptions);

        // Try to add the same route with ThrowOnDuplicate = true
        var duplicateOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-test",
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 201;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            HttpVerbs = [HttpVerb.Get]
        };
        _ = host.AddMapRoute(firstOptions);

        // Try to add the same route with ThrowOnDuplicate = false (default)
        var duplicateOptions = new MapRouteOptions
        {
            Pattern = "/duplicate-no-throw",
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 201;",
                Language = ScriptLanguage.CSharp
            },
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp,
                Arguments = []
            }
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options.ScriptCode, logger);

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
            ScriptCode = new LanguageOptions
            {
                Code = "$Context.Response.StatusCode = 200",
                Language = ScriptLanguage.PowerShell,
                Arguments = []
            }
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options.ScriptCode, logger);

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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = (ScriptLanguage)999, // Invalid language
                Arguments = []
            }
        };

        var logger = Serilog.Log.Logger;

        var ex = Assert.Throws<NotSupportedException>(() => KestrunHostMapExtensions.CompileScript(options.ScriptCode, logger));

        Assert.Contains("999", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CompileScript_WithVBNet_ReturnsRequestDelegate()
    {
        var options = new MapRouteOptions
        {
            Pattern = "/test",
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200",
                Language = ScriptLanguage.VBNet,
                Arguments = [],
                ExtraImports = [],
                ExtraRefs = []
            }
        };

        var logger = Serilog.Log.Logger;
        var compiled = KestrunHostMapExtensions.CompileScript(options.ScriptCode, logger);

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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            HttpVerbs = [HttpVerb.Get]
        };

        // Create a simple RequestDelegate
        static Task compiled(HttpContext context)
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        var result = KestrunHostMapExtensions.CreateAndRegisterRoute(host, routeOptions, compiled);

        Assert.NotNull(result);
        Assert.True(host.MapExists("/test-create-register", HttpVerb.Get));

        var savedOptions = host.GetMapRouteOptions("/test-create-register", HttpVerb.Get);
        Assert.NotNull(savedOptions);
        Assert.Equal(routeOptions.Pattern, savedOptions!.Pattern);
        Assert.Equal(routeOptions.ScriptCode.Language, savedOptions.ScriptCode.Language);
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
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            HttpVerbs = [HttpVerb.Get, HttpVerb.Post, HttpVerb.Put]
        };

        static Task compiled(HttpContext context)
        {
            context.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        var result = KestrunHostMapExtensions.CreateAndRegisterRoute(host, routeOptions, compiled);

        Assert.NotNull(result);
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Get));
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Post));
        Assert.True(host.MapExists("/test-multi-verbs", HttpVerb.Put));
        Assert.False(host.MapExists("/test-multi-verbs", HttpVerb.Delete));
    }

    #endregion

    #region TryParseEndpointSpec Tests

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("https://localhost:5000", "localhost", 5000, true)]
    [InlineData("http://localhost:5000", "localhost", 5000, false)]
    [InlineData("https://127.0.0.1:8080", "127.0.0.1", 8080, true)]
    [InlineData("http://127.0.0.1:8080", "127.0.0.1", 8080, false)]
    [InlineData("https://[::1]:5000", "[::1]", 5000, true)]
    [InlineData("http://[::1]:5000", "[::1]", 5000, false)]
    [InlineData("https://example.com:443", "example.com", 443, true)]
    [InlineData("http://example.com:80", "example.com", 80, false)]
    [InlineData("https://localhost", "localhost", 443, true)]
    [InlineData("http://localhost", "localhost", 80, false)]
    public void TryParseEndpointSpec_WithValidUrls_ReturnsTrue(string spec, string expectedHost, int expectedPort, bool expectedHttps)
    {
        var result = KestrunHostMapExtensions.TryParseEndpointSpec(spec, out var host, out var port, out var https);

        Assert.True(result);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
        Assert.Equal(expectedHttps, https);
    }

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("localhost:5000", "localhost", 5000, null)]
    [InlineData("127.0.0.1:8080", "127.0.0.1", 8080, null)]
    [InlineData("[::1]:5000", "::1", 5000, null)]
    [InlineData("[2001:db8::1]:8080", "2001:db8::1", 8080, null)]
    [InlineData("example.com:443", "example.com", 443, null)]
    [InlineData("192.168.1.1:3000", "192.168.1.1", 3000, null)]
    public void TryParseEndpointSpec_WithHostPort_ReturnsTrue(string spec, string expectedHost, int expectedPort, bool? expectedHttps)
    {
        var result = KestrunHostMapExtensions.TryParseEndpointSpec(spec, out var host, out var port, out var https);

        Assert.True(result);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
        Assert.Equal(expectedHttps, https);
    }

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")] // Missing port
    [InlineData(":5000")] // Missing host
    [InlineData("localhost:abc")] // Invalid port
    [InlineData("localhost:-1")] // Negative port
    [InlineData("localhost:0")] // Zero port
    [InlineData("ftp://localhost:5000")] // Unsupported scheme
    [InlineData("localhost:5000:extra")] // Extra parts
    [InlineData("[::1:5000")] // Malformed IPv6
    [InlineData("::1]:5000")] // Malformed IPv6
    [InlineData("https://")] // Incomplete URL
    [InlineData("https://localhost:")] // URL with empty port
    public void TryParseEndpointSpec_WithInvalidSpecs_ReturnsFalse(string spec)
    {
        var result = KestrunHostMapExtensions.TryParseEndpointSpec(spec, out var host, out var port, out var https);

        Assert.False(result);
        Assert.Equal("", host);
        Assert.Equal(0, port);
        Assert.Null(https);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void TryParseEndpointSpec_WithNullSpec_ReturnsFalse()
    {
        var result = KestrunHostMapExtensions.TryParseEndpointSpec(null!, out var host, out var port, out var https);

        Assert.False(result);
        Assert.Equal("", host);
        Assert.Equal(0, port);
        Assert.Null(https);
    }

    #endregion

    #region ToRequireHost Tests

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("localhost", 5000, "localhost:5000")]
    [InlineData("example.com", 8080, "example.com:8080")]
    [InlineData("127.0.0.1", 3000, "127.0.0.1:3000")]
    [InlineData("192.168.1.1", 443, "192.168.1.1:443")]
    public void ToRequireHost_WithIPv4AndHostnames_FormatsCorrectly(string host, int port, string expected)
    {
        var result = KestrunHostMapExtensions.ToRequireHost(host, port);

        Assert.Equal(expected, result);
    }

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("::1", 5000, "[::1]:5000")]
    [InlineData("2001:db8::1", 8080, "[2001:db8::1]:8080")]
    [InlineData("fe80::1", 443, "[fe80::1]:443")]
    [InlineData("::ffff:192.0.2.1", 3000, "[::ffff:192.0.2.1]:3000")]
    public void ToRequireHost_WithIPv6Addresses_AddsSquareBrackets(string host, int port, string expected)
    {
        var result = KestrunHostMapExtensions.ToRequireHost(host, port);

        Assert.Equal(expected, result);
    }

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("not-an-ip", 5000, "not-an-ip:5000")]
    [InlineData("invalid.ip.address", 8080, "invalid.ip.address:8080")]
    [InlineData("", 3000, ":3000")]
    public void ToRequireHost_WithNonIPAddresses_FormatsWithoutBrackets(string host, int port, string expected)
    {
        var result = KestrunHostMapExtensions.ToRequireHost(host, port);

        Assert.Equal(expected, result);
    }

    #endregion

    #region ApplyRequiredHost Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithNoEndpoints_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var map = host.AddMapRoute("/test", HttpVerb.Get, "Context.Response.StatusCode = 200;", ScriptLanguage.CSharp);

        // Should not throw
        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithEmptyEndpoints_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var map = host.AddMapRoute("/test", HttpVerb.Get, "Context.Response.StatusCode = 200;", ScriptLanguage.CSharp);

        // Should not throw
        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithValidEndpoint_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["127.0.0.1:5000"]
        };

        var map = host.AddMapRoute(options);

        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithValidHttpsEndpoint_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        // Create a self-signed certificate for testing
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _ = host.ConfigureListener(5001, System.Net.IPAddress.Parse("127.0.0.1"), cert);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-https",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["https://127.0.0.1:5001"]
        };

        var map = host.AddMapRoute(options);

        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithMatchingAnyListener_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Any, null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false); // Listen on Any
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-any",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["127.0.0.1:5000"] // Request specific IP
        };

        var map = host.AddMapRoute(options);

        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithInvalidEndpointFormat_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-invalid",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["invalid-format"]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));

        Assert.Contains("must be 'host:port' or 'http(s)://host:port'", ex.Message);
        Assert.Contains("invalid-format", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithNonMatchingListener_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-no-match",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["127.0.0.1:8080"] // Different port
        };

        var ex = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));

        Assert.Contains("doesn't match any configured listener", ex.Message);
        Assert.Contains("127.0.0.1:8080", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithMultipleValidEndpoints_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);

        // Create a self-signed certificate for HTTPS testing
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _ = host.ConfigureListener(5001, System.Net.IPAddress.Parse("127.0.0.1"), cert);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-multiple",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["127.0.0.1:5000", "https://127.0.0.1:5001"]
        };

        var map = host.AddMapRoute(options);

        Assert.NotNull(map);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithMixedValidAndInvalidEndpoints_ThrowsArgumentException()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.Parse("127.0.0.1"), null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-mixed",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["127.0.0.1:5000", "invalid-endpoint", "127.0.0.1:9999"]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => host.AddMapRoute(options));

        Assert.Contains("Invalid Endpoints:", ex.Message);
        Assert.Contains("invalid-endpoint", ex.Message);
        Assert.Contains("127.0.0.1:9999", ex.Message);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyRequiredHost_WithIPv6Endpoint_DoesNotThrow()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.ConfigureListener(5000, System.Net.IPAddress.IPv6Loopback, null, Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1, false);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/test-ipv6",
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Code = "Context.Response.StatusCode = 200;",
                Language = ScriptLanguage.CSharp
            },
            Endpoints = ["[::1]:5000"]
        };

        var map = host.AddMapRoute(options);

        Assert.NotNull(map);
    }

    #endregion
}
