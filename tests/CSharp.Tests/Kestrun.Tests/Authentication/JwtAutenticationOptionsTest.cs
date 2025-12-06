using System.Security.Claims;
using Kestrun.Authentication;
using Kestrun.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrunTests.Authentication;

public class JwtAuthenticationOptionsTest
{
    [Fact]
    public void Can_Set_And_Get_ValidationParameters()
    {
        var options = new JwtAuthOptions();
        var parameters = new TokenValidationParameters();
        options.ValidationParameters = parameters;
        Assert.Same(parameters, options.ValidationParameters);
    }

    [Fact]
    public void Can_Set_And_Get_ClaimPolicy()
    {
        var options = new JwtAuthOptions();
        var policy = new ClaimPolicyConfig();
        options.ClaimPolicy = policy;
        Assert.Same(policy, options.ClaimPolicy);
    }

    [Fact]
    public void IssueClaims_Default_Is_Null()
    {
        var options = new JwtAuthOptions();
        Assert.Null(options.IssueClaims);
    }

    [Fact]
    public async Task Can_Set_And_Invoke_IssueClaims()
    {
        var options = new JwtAuthOptions();
        var httpContext = new DefaultHttpContext();
        options.IssueClaims = (_, _) =>
        {
            return Task.FromResult<IEnumerable<Claim>>([new Claim("type", "value")]);
        };

        var claims = await options.IssueClaims(httpContext, "user");
        var list = new List<Claim>(claims);
        _ = Assert.Single(list);
        Assert.Equal("type", list[0].Type);
    }

    [Fact]
    public void IssueClaimsCodeSettings_Default_NotNull()
    {
        var options = new JwtAuthOptions();
        Assert.NotNull(options.IssueClaimsCodeSettings);
    }

    [Fact]
    public void Can_Set_And_Get_ClaimPolicyConfig()
    {
        var options = new JwtAuthOptions();
        var config = new ClaimPolicyConfig();
        options.ClaimPolicyConfig = config;
        Assert.Same(config, options.ClaimPolicyConfig);
    }
}
