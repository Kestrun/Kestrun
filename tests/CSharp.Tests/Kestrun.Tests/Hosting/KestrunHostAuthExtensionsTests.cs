using System.Security.Claims;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Kestrun.Authentication;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Microsoft.AspNetCore.Http;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Serilog;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostAuthExtensionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void ClientCertificate_Adds_Scheme_And_RegistryEntry()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddClientCertificateAuthentication(
            scheme: "CertificateX",
            displayName: "Client Certificate Authentication",
            configure: _ => { });

        _ = host.Build();

        Assert.True(host.HasAuthScheme("CertificateX"));
        Assert.True(host.RegisteredAuthentications.Exists("CertificateX", AuthenticationType.Certificate));

        Assert.True(host.RegisteredAuthentications.TryGet<ClientCertificateAuthenticationOptions>(
            "CertificateX",
            AuthenticationType.Certificate,
            out var opts));
        Assert.NotNull(opts);
        Assert.Equal(host, opts!.Host);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task JwtBearer_Adds_Scheme_And_Policies()
    {
        var host = new KestrunHost("TestApp");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)));
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
                ["JwtUsersOnly"] = new ClaimRule(ClaimTypes.Name, "Alice")
            }
        };

        _ = host.AddJwtBearerAuthentication("BearerX", displayName: "BearerX", configureOptions: new JwtAuthOptions
        {
            Host = host,
            TokenValidationParameters = tvp,
            ClaimPolicy = cfg
        });

        var app = host.Build();

        Assert.True(host.HasAuthScheme("BearerX"));
        Assert.True(host.HasAuthPolicy("JwtUsersOnly"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("JwtUsersOnly");
        Assert.NotNull(policy);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void JwtBearer_Omitted_ClaimPolicies_Registers_No_Custom_Policy()
    {
        var host = new KestrunHost("TestApp");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)));
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        _ = host.AddJwtBearerAuthentication("BearerNoPolicy", displayName: "BearerNoPolicy", configureOptions: new JwtAuthOptions
        {
            Host = host,
            TokenValidationParameters = tvp,
        });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("BearerNoPolicy"));
        Assert.False(host.HasAuthPolicy("SomeMissingPolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task Cookie_Adds_Scheme_And_Policies()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["CookieMustBeAdmin"] = new ClaimRule(ClaimTypes.Role, "Admin")
            }
        };

        _ = host.AddCookieAuthentication("CookieX", configureOptions: _ => { }, claimPolicy: cfg);

        var app = host.Build();

        Assert.True(host.HasAuthScheme("CookieX"));
        Assert.True(host.HasAuthPolicy("CookieMustBeAdmin"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("CookieMustBeAdmin");
        Assert.NotNull(policy);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Cookie_Omitted_ClaimPolicies_Registers_No_Custom_Policy()
    {
        var host = new KestrunHost("TestApp");
        _ = host.AddCookieAuthentication("CookieNoPolicy", "CookieNoPolicy", _ => { });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("CookieNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentCookiePolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Windows_Negotiate_Adds_Scheme()
    {
        var host = new KestrunHost("TestApp");
        _ = host.AddWindowsAuthentication();
        _ = host.Build();

        Assert.True(host.HasAuthScheme(NegotiateDefaults.AuthenticationScheme));
    }


    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_ObjectOverload_Copies_Options_And_Adds_Policies()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["MustBeUserCharlie"] = new ClaimRule(ClaimTypes.Name, "Charlie")
            }
        };

        var opts = new BasicAuthenticationOptions
        {
            HeaderName = "Authorization",
            Base64Encoded = true,
            Realm = "realm",
            AllowInsecureHttp = false,
            SuppressWwwAuthenticate = false,
            ClaimPolicyConfig = cfg
        };

        _ = host.AddBasicAuthentication("BasicY", "Basic Authentication for Y", opts);
        var app = host.Build();

        Assert.True(host.HasAuthScheme("BasicY"));
        Assert.True(host.HasAuthPolicy("MustBeUserCharlie"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("MustBeUserCharlie");
        Assert.NotNull(policy);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BasicAuth_Omitted_ClaimPolicies_Registers_No_Custom_Policy()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddBasicAuthentication("BasicNoPolicy", "Basic Authentication with No Policy", _ => { });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("BasicNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentBasicPolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_CSharp_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddBasicAuthentication("BasicCode", "CSharp Basic Authentication", opts =>
        {
            opts.ValidateCodeSettings = new AuthenticationCodeSettings
            {
                Language = Kestrun.Scripting.ScriptLanguage.CSharp,
                Code = "username == \"bob\" && password == \"secret\""
            };

            opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
            {
                Language = Kestrun.Scripting.ScriptLanguage.CSharp,
                Code = "new [] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, identity) }"
            };
        });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<BasicAuthenticationOptions>>();
        var options = monitor.Get("BasicCode");

        var ctx = new DefaultHttpContext();
        var valid = await options.ValidateCredentialsAsync(ctx, "bob", "secret");
        var invalid = await options.ValidateCredentialsAsync(ctx, "bob", "wrong");
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "alice");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "alice");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKey_ObjectOverload_Copies_Options_And_Adds_Policies()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["ApiKeyNamedDana"] = new ClaimRule(ClaimTypes.Name, "Dana")
            }
        };

        var opts = new ApiKeyAuthenticationOptions
        {
            StaticApiKey = "abc",
            ApiKeyName = "X-API-KEY",
            AllowQueryStringFallback = true,
            AllowInsecureHttp = false,
            EmitChallengeHeader = false,
            ClaimPolicyConfig = cfg
        };

        _ = host.AddApiKeyAuthentication("ApiKeyY", "API Key Authentication for Y", opts);
        var app = host.Build();

        Assert.True(host.HasAuthScheme("ApiKeyY"));
        Assert.True(host.HasAuthPolicy("ApiKeyNamedDana"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("ApiKeyNamedDana");
        Assert.NotNull(policy);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApiKey_Omitted_ClaimPolicies_Registers_No_Custom_Policy()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddApiKeyAuthentication("ApiKeyNoPolicy", "API Key Authentication with No Policy", _ => { });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("ApiKeyNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentApiKeyPolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKey_CSharp_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddApiKeyAuthentication("ApiKeyCode", "CSharp API Key Authentication", opts =>
        {
            opts.ValidateCodeSettings = new AuthenticationCodeSettings
            {
                Language = Kestrun.Scripting.ScriptLanguage.CSharp,
                Code = "providedKey == \"abc\""
            };

            opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
            {
                Language = Kestrun.Scripting.ScriptLanguage.CSharp,
                Code = "new [] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, identity) }"
            };
        });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        var options = monitor.Get("ApiKeyCode");

        var ctx = new DefaultHttpContext();
        var valid = await options.ValidateKeyAsync(ctx, "abc", Encoding.UTF8.GetBytes("abc"));
        var invalid = await options.ValidateKeyAsync(ctx, "nope", Encoding.UTF8.GetBytes("nope"));
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "client1");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "client1");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_PowerShell_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddBasicAuthentication("BasicPS", "PowerShell Basic Authentication", opts =>
            {
                opts.ValidateCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.PowerShell,
                    Code = "param($username,$password) $username -eq 'bob' -and $password -eq 'secret'"
                };

                opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.PowerShell,
                    Code = "param($identity) ,@{ Type = [System.Security.Claims.ClaimTypes]::Name; Value = $identity }"
                };
            });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<BasicAuthenticationOptions>>();
        var options = monitor.Get("BasicPS");

        using var rs = RunspaceFactory.CreateRunspace();
        rs.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = rs;

        var ctx = new DefaultHttpContext();
        ctx.Items["PS_INSTANCE"] = ps;

        var valid = await options.ValidateCredentialsAsync(ctx, "bob", "secret");
        var invalid = await options.ValidateCredentialsAsync(ctx, "bob", "wrong");
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "alice");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "alice");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKey_PowerShell_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddApiKeyAuthentication("ApiKeyPS", "PowerShell API Key Authentication", opts =>
            {
                opts.ValidateCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.PowerShell,
                    Code = "param($providedKey,$providedKeyBytes) $providedKey -eq 'abc'"
                };

                opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.PowerShell,
                    Code = "param($identity) ,@{ Type = [System.Security.Claims.ClaimTypes]::Name; Value = $identity }"
                };
            });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        var options = monitor.Get("ApiKeyPS");

        using var rs = RunspaceFactory.CreateRunspace();
        rs.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = rs;

        var ctx = new DefaultHttpContext();
        ctx.Items["PS_INSTANCE"] = ps;

        var valid = await options.ValidateKeyAsync(ctx, "abc", Encoding.UTF8.GetBytes("abc"));
        var invalid = await options.ValidateKeyAsync(ctx, "nope", Encoding.UTF8.GetBytes("nope"));
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "client1");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "client1");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_VBNet_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddBasicAuthentication("BasicVB", "VBNet Basic Authentication", opts =>
            {
                opts.ValidateCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.VBNet,
                    Code = "Return username = \"bob\" AndAlso password = \"secret\""
                };

                opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.VBNet,
                    Code = "Dim l = New System.Collections.Generic.List(Of System.Security.Claims.Claim)() : l.Add(New System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, identity)) : Return l"
                };
            });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<BasicAuthenticationOptions>>();
        var options = monitor.Get("BasicVB");

        var ctx = new DefaultHttpContext();
        var valid = await options.ValidateCredentialsAsync(ctx, "bob", "secret");
        var invalid = await options.ValidateCredentialsAsync(ctx, "bob", "wrong");
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "alice");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "alice");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKey_VBNet_Validator_And_IssueClaims_Wiring_Works()
    {
        var host = new KestrunHost("TestApp");

        _ = host.AddApiKeyAuthentication("ApiKeyVB", "VBNet API Key Authentication", opts =>
            {
                opts.ValidateCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.VBNet,
                    Code = "Return providedKey = \"abc\""
                };

                opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
                {
                    Language = Kestrun.Scripting.ScriptLanguage.VBNet,
                    Code = "Dim l = New System.Collections.Generic.List(Of System.Security.Claims.Claim)() : l.Add(New System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, identity)) : Return l"
                };
            });

        var app = host.Build();
        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        var options = monitor.Get("ApiKeyVB");

        var ctx = new DefaultHttpContext();
        var valid = await options.ValidateKeyAsync(ctx, "abc", Encoding.UTF8.GetBytes("abc"));
        var invalid = await options.ValidateKeyAsync(ctx, "nope", Encoding.UTF8.GetBytes("nope"));
        Assert.True(valid);
        Assert.False(invalid);

        var claims = await options.IssueClaims!(ctx, "client1");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "client1");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OAuth2_Adds_Scheme_And_DefaultPolicyFromScopes()
    {
        var host = new KestrunHost("TestApp");
        var options = new OAuth2Options
        {
            ClientId = "client-id",
            ClientSecret = "secret",
            AuthorizationEndpoint = "https://auth.example/authorize",
            TokenEndpoint = "https://auth.example/token",
        };
        options.Scope.Add("openid");
        options.Scope.Add("profile");

        _ = host.AddOAuth2Authentication("OAuth2X", "OAuth2 X", options);
        _ = host.Build();

        Assert.True(host.HasAuthScheme("OAuth2X"));
        Assert.True(host.HasAuthPolicy("openid"));
        Assert.True(host.HasAuthPolicy("profile"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OAuth2_Throws_When_ClientIdMissing()
    {
        var host = new KestrunHost("TestApp");
        var options = new OAuth2Options
        {
            ClientId = "",
            AuthorizationEndpoint = "https://auth.example/authorize",
            TokenEndpoint = "https://auth.example/token",
        };

        _ = Assert.Throws<ArgumentException>(() => host.AddOAuth2Authentication("OAuth2MissingClient", configureOptions: options));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OpenIdConnect_WithJwkJson_ConfiguresEventsType()
    {
        var host = new KestrunHost("TestApp");
        var options = new OidcOptions
        {
            ClientId = "oidc-client",
            ClientSecret = "oidc-secret",
            Authority = "https://example.invalid",
            JwkJson = "{\"kty\":\"oct\",\"k\":\"AQAB\"}",
        };
        options.Scope.Add("openid");

        _ = host.AddOpenIdConnectAuthentication("OidcX", "OIDC X", options);
        var app = host.Build();

        var monitor = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>>();
        var configured = monitor.Get("OidcX");

        Assert.True(host.HasAuthScheme("OidcX"));
        Assert.Equal(typeof(OidcEvents), configured.EventsType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetSupportedScopes_WithLocalOidcMetadata_ReturnsPolicies()
    {
        var listener = new HttpListener();
        var port = GetFreeTcpPort();
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        var metadata = $$"""
            {
              "issuer": "{{prefix.TrimEnd('/')}}",
              "authorization_endpoint": "{{prefix}}authorize",
              "token_endpoint": "{{prefix}}token",
              "jwks_uri": "{{prefix}}jwks",
              "scopes_supported": ["openid", "profile", "email"]
            }
            """;

        var serveTask = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < 3 && listener.IsListening; i++)
                {
                    var context = await listener.GetContextAsync();
                    var requestPath = context.Request.Url?.AbsolutePath ?? string.Empty;
                    string body;

                    if (requestPath.Contains("/.well-known/openid-configuration", StringComparison.Ordinal))
                    {
                        body = metadata;
                    }
                    else if (requestPath.Contains("/jwks", StringComparison.Ordinal))
                    {
                        body = "{\"keys\":[]}";
                    }
                    else
                    {
                        body = "{}";
                    }

                    var responseBytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = responseBytes.Length;
                    await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                }
            }
            catch
            {
                // Listener teardown path.
            }
        });

        try
        {
            var method = typeof(KestrunHostAuthnExtensions).GetMethod("GetSupportedScopes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var claimPolicy = method!.Invoke(null, [prefix.TrimEnd('/'), Serilog.Log.Logger]) as ClaimPolicyConfig;
            Assert.NotNull(claimPolicy);
            Assert.Contains("openid", claimPolicy!.PolicyNames);
            Assert.Contains("profile", claimPolicy.PolicyNames);
            Assert.Contains("email", claimPolicy.PolicyNames);
        }
        finally
        {
            listener.Stop();
            serveTask.GetAwaiter().GetResult();
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task LoggingHttpMessageHandler_CapturesTokenResponse_AndPreservesStream()
    {
        var handlerType = typeof(KestrunHostAuthnExtensions).GetNestedType("LoggingHttpMessageHandler", BindingFlags.NonPublic);
        Assert.NotNull(handlerType);

        const string payload = "{\"access_token\":\"abc123\",\"token_type\":\"Bearer\"}";
        using var inner = new StubMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            return response;
        });

        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(new NullSink()).CreateLogger();
        using var loggingHandler = (HttpMessageHandler)Activator.CreateInstance(handlerType!, inner, logger)!;
        using var client = new HttpClient(loggingHandler) { BaseAddress = new Uri("https://example.invalid/") };

        using var response = await client.PostAsync("connect/token", new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(payload, body);

        var lastBodyProperty = handlerType!.GetProperty("LastTokenResponseBody", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(lastBodyProperty);
        Assert.Equal(payload, lastBodyProperty!.GetValue(null)?.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task LoggingHttpMessageHandler_LogsAndPreservesNonTokenResponses()
    {
        var handlerType = typeof(KestrunHostAuthnExtensions).GetNestedType("LoggingHttpMessageHandler", BindingFlags.NonPublic);
        Assert.NotNull(handlerType);

        const string payload = "{\"status\":\"ok\"}";
        using var inner = new StubMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            return response;
        });

        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(new NullSink()).CreateLogger();
        using var loggingHandler = (HttpMessageHandler)Activator.CreateInstance(handlerType!, inner, logger)!;
        using var client = new HttpClient(loggingHandler) { BaseAddress = new Uri("https://example.invalid/") };

        using var request = new HttpRequestMessage(HttpMethod.Post, "userinfo")
        {
            Content = new StringContent("client=demo", Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(payload, body);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class NullSink : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
        }
    }
}
