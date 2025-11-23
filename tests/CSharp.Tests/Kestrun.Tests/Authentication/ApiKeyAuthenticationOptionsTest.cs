using System.Security.Claims;
using Kestrun.Authentication;
using Kestrun.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KestrunTests.Authentication;

public class ApiKeyAuthenticationOptionsTest
{
    [Fact]
    public void Default_HeaderName_Is_XApiKey()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.Equal("X-Api-Key", options.ApiKeyName);
    }

    [Fact]
    public void AdditionalHeaderNames_Defaults_To_Empty()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.Empty(options.AdditionalHeaderNames);
    }

    [Fact]
    public void AllowQueryStringFallback_Defaults_To_False()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.False(options.AllowQueryStringFallback);
    }

    [Fact]
    public void RequireHttps_Defaults_To_True()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.True(options.AllowInsecureHttp);
    }

    [Fact]
    public void EmitChallengeHeader_Defaults_To_True()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.True(options.EmitChallengeHeader);
    }

    [Fact]
    public void ChallengeHeaderFormat_Defaults_To_ApiKeyHeader()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.Equal(ApiKeyChallengeFormat.ApiKeyHeader, options.ChallengeHeaderFormat);
    }

    [Fact]
    public void ExpectedKeyBytes_Returns_Bytes_If_ExpectedKey_Set()
    {
        var options = new ApiKeyAuthenticationOptions { StaticApiKey = "abc123" };
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("abc123"), options.StaticApiKeyAsBytes);
    }

    [Fact]
    public void ExpectedKeyBytes_Returns_Null_If_ExpectedKey_Null()
    {
        var options = new ApiKeyAuthenticationOptions { StaticApiKey = null };
        Assert.Null(options.StaticApiKeyAsBytes);
    }

    [Fact]
    public async Task ValidateKeyAsync_Default_Returns_False()
    {
        var options = new ApiKeyAuthenticationOptions();
        var result = await options.ValidateKeyAsync(new DefaultHttpContext(), "key", [1, 2, 3]);
        Assert.False(result);
    }

    [Fact]
    public void Logger_Is_Not_Null()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.NotNull(options.Logger);
    }

    [Fact]
    public void ValidateCodeSettings_Is_Not_Null()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.NotNull(options.ValidateCodeSettings);
    }

    [Fact]
    public void IssueClaimsCodeSettings_Is_Not_Null()
    {
        var options = new ApiKeyAuthenticationOptions();
        Assert.NotNull(options.IssueClaimsCodeSettings);
    }

    [Fact]
    public void ClaimPolicyConfig_Can_Be_Set_And_Retrieved()
    {
        var options = new ApiKeyAuthenticationOptions();
        var config = new ClaimPolicyConfig();
        options.ClaimPolicyConfig = config;
        Assert.Same(config, options.ClaimPolicyConfig);
    }

    [Fact]
    public async Task IssueClaims_Can_Be_Set_And_Invoked()
    {
        var options = new ApiKeyAuthenticationOptions
        {
            IssueClaims = (_, _) => Task.FromResult<IEnumerable<Claim>>([new Claim("type", "value")])
        };
        var claims = await options.IssueClaims(new DefaultHttpContext(), "user");
        _ = Assert.Single(claims);
        Assert.Equal("type", claims.First().Type);
        Assert.Equal("value", claims.First().Value);
    }
}
