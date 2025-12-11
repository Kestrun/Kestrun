using System.Security.Claims;
using System.Text;
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

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostAuthExtensionsTests
{
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
}
