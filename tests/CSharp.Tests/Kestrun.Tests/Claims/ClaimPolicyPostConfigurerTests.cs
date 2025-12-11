using System.Security.Claims;
using Kestrun.Authentication;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace KestrunTests.Claims;

public class ClaimPolicyPostConfigurerTests
{
    private sealed class TestClaimsCommonOptions : IClaimsCommonOptions
    {
        public Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get; set; }
        public AuthenticationCodeSettings IssueClaimsCodeSettings { get; set; } = new();
        public ClaimPolicyConfig? ClaimPolicyConfig { get; set; }
    }

    private sealed class TestOptionsMonitor<T>(Func<string, T> getter) : IOptionsMonitor<T>
    {
        private readonly Func<string, T> _getter = getter;

        public T CurrentValue => _getter(Options.DefaultName);
        public T Get(string? name) => _getter(name ?? Options.DefaultName);
        public IDisposable OnChange(Action<T, string> listener) => new DummyDisposable();

        private sealed class DummyDisposable : IDisposable { public void Dispose() { } }
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void PostConfigure_AppliesPolicies_From_MonitoredOptions()
    {
        var scheme = "BasicX";
        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["P1"] = new ClaimRule(ClaimTypes.Role, string.Empty, "Admin"),
                ["P2"] = new ClaimRule(ClaimTypes.Name, string.Empty, "Alice")
            }
        };

        var monitored = new TestOptionsMonitor<IClaimsCommonOptions>(name =>
            new TestClaimsCommonOptions { ClaimPolicyConfig = name == scheme ? cfg : null });

        var post = new ClaimPolicyPostConfigurer(scheme, monitored);
        var options = new AuthorizationOptions();

        post.PostConfigure(null, options);

        var p1 = options.GetPolicy("P1");
        Assert.NotNull(p1);
        var req1 = Assert.Single(p1!.Requirements);
        var claimReq1 = Assert.IsType<Microsoft.AspNetCore.Authorization.Infrastructure.ClaimsAuthorizationRequirement>(req1);
        Assert.Equal(ClaimTypes.Role, claimReq1.ClaimType);

#pragma warning disable IDE0305,CA1861
        Assert.Equal(new[] { "Admin" }, claimReq1.AllowedValues!.ToArray());
#pragma warning restore IDE0305,CA1861

        Assert.NotNull(options.GetPolicy("P2"));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void PostConfigure_NoClaimPolicyConfig_DoesNothing()
    {
        var scheme = "BasicY";
        var monitored = new TestOptionsMonitor<IClaimsCommonOptions>(_ => new TestClaimsCommonOptions { ClaimPolicyConfig = null });

        var post = new ClaimPolicyPostConfigurer(scheme, monitored);
        var options = new AuthorizationOptions();

        post.PostConfigure(null, options);

        Assert.Null(options.GetPolicy("Anything"));
    }
}
