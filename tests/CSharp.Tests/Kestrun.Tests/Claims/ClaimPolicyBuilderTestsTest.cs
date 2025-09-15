// csharp
using System.Security.Claims;
using Kestrun.Claims;
using Xunit;

namespace KestrunTests.Claims;

public class ClaimPolicyBuilderTestsTest
{
    private static readonly string[] ManagerAllowed = ["Manager"];

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_WithEnum_Uses_ToClaimUri_Mapping_UsesStaticArray()
    {
        var builder = new ClaimPolicyBuilder()
            .AddPolicy("RolePolicy", UserIdentityClaim.Role, ManagerAllowed);

        var rule = builder.Policies["RolePolicy"];
        Assert.Equal(ClaimTypes.Role, rule.ClaimType);
        Assert.Equal(ManagerAllowed, rule.AllowedValues);
    }
}
