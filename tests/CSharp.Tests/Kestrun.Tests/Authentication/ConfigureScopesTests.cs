using System.Reflection;
using Kestrun.Authentication;
using Kestrun.Claims;
using Kestrun.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Authentication;

public class ConfigureScopesTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    [Fact]
    [Trait("Category", "Authentication")]
    public void BuildsClaimPolicyWhenNoneProvided()
    {
        var options = new OAuth2Options();
        options.Scope.Add("read");
        options.Scope.Add("write");

        InvokeConfigureScopes(options);

        var claimPolicy = Assert.IsType<ClaimPolicyConfig>(options.ClaimPolicy);
        Assert.True(claimPolicy.Policies.ContainsKey("read"));
        Assert.True(claimPolicy.Policies.ContainsKey("write"));
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void AddsMissingScopesToExistingClaimPolicy()
    {
        var options = new OAuth2Options();
        var claimPolicyBuilder = new ClaimPolicyBuilder();
        _ = claimPolicyBuilder.AddPolicy("existing", "scope", string.Empty, "existing");
        options.ClaimPolicy = claimPolicyBuilder.Build();
        options.Scope.Add("existing");
        options.Scope.Add("new-scope");

        InvokeConfigureScopes(options);

        var claimPolicy = Assert.IsType<ClaimPolicyConfig>(options.ClaimPolicy);
        Assert.True(claimPolicy.Policies.ContainsKey("existing"));
        Assert.True(claimPolicy.Policies.ContainsKey("new-scope"));
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void BackfillsScopesFromClaimPolicyWhenScopeEmpty()
    {
        var options = new OAuth2Options();
        options.Scope.Clear();
        var claimPolicyBuilder = new ClaimPolicyBuilder();
        _ = claimPolicyBuilder.AddPolicy("one", "scope", string.Empty, "one");
        _ = claimPolicyBuilder.AddPolicy("two", "scope", string.Empty, "two");
        options.ClaimPolicy = claimPolicyBuilder.Build();

        InvokeConfigureScopes(options);

        Assert.Contains("one", options.Scope);
        Assert.Contains("two", options.Scope);
    }

    private static void InvokeConfigureScopes(IOAuthCommonOptions options)
    {
        var method = typeof(KestrunHostAuthnExtensions)
            .GetMethod("ConfigureScopes", BindingFlags.NonPublic | BindingFlags.Static)!;

        object[] parameters = [options, Logger];
        _ = method.Invoke(null, parameters);
    }

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
        }
    }
}
