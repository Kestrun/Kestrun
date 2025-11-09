using System.Security.Claims;
using System.Text;
using Kestrun.Authentication;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;
using Microsoft.AspNetCore.Http;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.SharedState;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostAuthExtensionsTests
{
    private static void SanitizeSharedGlobals()
    {
        // Ensure any leftover globals use 'object' typing in script prelude
        foreach (var key in SharedStateStore.KeySnapshot())
        {
            _ = SharedStateStore.Set(key, null);
        }
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

        _ = host.AddJwtBearerAuthentication("BearerX", tvp, claimPolicy: cfg);

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

        _ = host.AddJwtBearerAuthentication("BearerNoPolicy", tvp);
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

        _ = host.AddCookieAuthentication("CookieX", configure: _ => { }, claimPolicy: cfg);

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
        _ = host.AddCookieAuthentication("CookieNoPolicy", _ => { });
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
    public void OpenIdConnect_Adds_Scheme()
    {
        var host = new KestrunHost("TestApp");
        var opts = new OpenIdConnectOptions
        {
            Authority = "https://example.com",
            ClientId = "client",
            ClientSecret = "secret",
            CallbackPath = "/signin-custom",
            ResponseType = OpenIdConnectResponseType.Code,
            UsePkce = true,
            SaveTokens = true,
            GetClaimsFromUserInfoEndpoint = true
        };
        opts.Scope.Add("email");

        _ = host.AddOpenIdConnectAuthentication("OidcX", opts);
        _ = host.Build();

        Assert.True(host.HasAuthScheme("OidcX"));
        Assert.True(host.HasAuthScheme("OidcX.Cookies"));
        Assert.True(host.HasAuthScheme("OidcX.Policy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OAuth2_Adds_Schemes_And_Cookie_PolicyScheme()
    {
        var host = new KestrunHost("TestApp");
        var opts = new Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            AuthorizationEndpoint = "https://example.com/oauth/authorize",
            TokenEndpoint = "https://example.com/oauth/token",
            CallbackPath = "/signin-oauth",
            UsePkce = true,
            SaveTokens = true
        };
        opts.Scope.Add("profile");
        opts.Scope.Add("email");
        // keep options minimal; claim mapping validated indirectly in handler

        _ = host.AddOAuth2Authentication("OAuthX", opts);
        _ = host.Build();

        Assert.True(host.HasAuthScheme("OAuthX"));
        Assert.True(host.HasAuthScheme("OAuthX.Policy"));
        Assert.True(host.HasAuthScheme("OAuthX.Cookies"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void OpenIdConnect_Omitted_ClaimPolicies_Registers_No_Custom_Policy()
    {
        var host = new KestrunHost("TestApp");
        var opts = new OpenIdConnectOptions
        {
            Authority = "https://example.com",
            ClientId = "client",
            ClientSecret = "secret"
        };
        _ = host.AddOpenIdConnectAuthentication("OidcNoPolicy", opts);
        _ = host.Build();

        Assert.True(host.HasAuthScheme("OidcNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentOidcPolicy"));
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
            RequireHttps = false,
            SuppressWwwAuthenticate = false,
            ClaimPolicyConfig = cfg
        };

        _ = host.AddBasicAuthentication("BasicY", opts);
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

        _ = host.AddBasicAuthentication("BasicNoPolicy", _ => { });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("BasicNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentBasicPolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task BasicAuth_CSharp_Validator_And_IssueClaims_Wiring_Works()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp");

        _ = host.AddBasicAuthentication("BasicCode", opts =>
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
            ExpectedKey = "abc",
            HeaderName = "X-API-KEY",
            AllowQueryStringFallback = true,
            RequireHttps = false,
            EmitChallengeHeader = false,
            ClaimPolicyConfig = cfg
        };

        _ = host.AddApiKeyAuthentication("ApiKeyY", opts);
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

        _ = host.AddApiKeyAuthentication("ApiKeyNoPolicy", _ => { });
        _ = host.Build();

        Assert.True(host.HasAuthScheme("ApiKeyNoPolicy"));
        Assert.False(host.HasAuthPolicy("NonExistentApiKeyPolicy"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ApiKey_CSharp_Validator_And_IssueClaims_Wiring_Works()
    {
        SanitizeSharedGlobals();
        var host = new KestrunHost("TestApp");

        _ = host.AddApiKeyAuthentication("ApiKeyCode", opts =>
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

        _ = host.AddBasicAuthentication("BasicPS", opts =>
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

        _ = host.AddApiKeyAuthentication("ApiKeyPS", opts =>
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

        _ = host.AddBasicAuthentication("BasicVB", opts =>
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

        _ = host.AddApiKeyAuthentication("ApiKeyVB", opts =>
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
}
