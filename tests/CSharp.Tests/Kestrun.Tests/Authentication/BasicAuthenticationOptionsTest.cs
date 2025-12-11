using System.Security.Claims;
using Kestrun.Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KestrunTests.Authentication;

public class BasicAuthenticationOptionsTest
{
    [Fact]
    public void Default_HeaderName_IsAuthorization()
    {
        var options = new BasicAuthenticationOptions();
        Assert.Equal("Authorization", options.HeaderName);
    }

    [Fact]
    public void Default_Base64Encoded_IsTrue()
    {
        var options = new BasicAuthenticationOptions();
        Assert.True(options.Base64Encoded);
    }

    [Fact]
    public void SeparatorRegex_Matches_UsernameAndPassword()
    {
        var options = new BasicAuthenticationOptions();
        var match = options.SeparatorRegex.Match("user:pass");
        Assert.True(match.Success);
        Assert.Equal("user", match.Groups[1].Value);
        Assert.Equal("pass", match.Groups[2].Value);
    }

    [Fact]
    public void Default_Realm_IsKestrun()
    {
        var options = new BasicAuthenticationOptions();
        Assert.Equal("Kestrun", options.Realm);
    }

    [Fact]
    public void Default_Logger_IsNotNull()
    {
        var options = new BasicAuthenticationOptions();
        Assert.NotNull(options.Logger);
    }

    [Fact]
    public void Default_RequireHttps_IsTrue()
    {
        var options = new BasicAuthenticationOptions();
        Assert.False(options.AllowInsecureHttp);
    }

    [Fact]
    public void Default_SuppressWwwAuthenticate_IsFalse()
    {
        var options = new BasicAuthenticationOptions();
        Assert.False(options.SuppressWwwAuthenticate);
    }

    [Fact]
    public async Task Default_ValidateCredentialsAsync_ReturnsFalse()
    {
        var options = new BasicAuthenticationOptions();
        var context = new DefaultHttpContext();
        var result = await options.ValidateCredentialsAsync(context, "user", "pass");
        Assert.False(result);
    }

    [Fact]
    public void Default_ValidateCodeSettings_IsNotNull()
    {
        var options = new BasicAuthenticationOptions();
        Assert.NotNull(options.ValidateCodeSettings);
    }

    [Fact]
    public void Default_IssueClaims_IsNull()
    {
        var options = new BasicAuthenticationOptions();
        Assert.Null(options.IssueClaims);
    }

    [Fact]
    public void Default_IssueClaimsCodeSettings_IsNotNull()
    {
        var options = new BasicAuthenticationOptions();
        Assert.NotNull(options.IssueClaimsCodeSettings);
    }

    [Fact]
    public void Default_ClaimPolicyConfig_IsNull()
    {
        var options = new BasicAuthenticationOptions();
        Assert.Null(options.ClaimPolicyConfig);
    }

    [Fact]
    public async Task IssueClaims_CanBeSet_AndReturnsClaims()
    {
        var options = new BasicAuthenticationOptions
        {
            IssueClaims = (_, _) => Task.FromResult<IEnumerable<Claim>>([new Claim("type", "value")])
        };
        var context = new DefaultHttpContext();
        var claims = await options.IssueClaims(context, "user");
        _ = Assert.Single(claims);
        Assert.Equal("type", claims.First().Type);
        Assert.Equal("value", claims.First().Value);
    }
}
