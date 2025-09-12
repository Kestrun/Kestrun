using System.Security.Claims;
using Kestrun.Claims;
using Xunit;

namespace KestrunTests.Claims;

public class UserIdentityClaimTests
{
    [Theory]
    [InlineData(UserIdentityClaim.Role, ClaimTypes.Role)]
    [InlineData(UserIdentityClaim.Name, ClaimTypes.Name)]
    [InlineData(UserIdentityClaim.NameIdentifier, ClaimTypes.NameIdentifier)]
    [InlineData(UserIdentityClaim.Upn, ClaimTypes.Upn)]
    [InlineData(UserIdentityClaim.PrimarySid, ClaimTypes.PrimarySid)]
    [InlineData(UserIdentityClaim.WindowsAccountName, ClaimTypes.WindowsAccountName)]
    [InlineData(UserIdentityClaim.Surname, ClaimTypes.Surname)]
    [InlineData(UserIdentityClaim.GivenName, ClaimTypes.GivenName)]
    public void ToClaimUri_ReturnsExpected_ForCoreMappings(UserIdentityClaim input, string expected) => Assert.Equal(expected, input.ToClaimUri());

    [Fact]
    [Trait("Category", "Claims")]
    public void Email_And_EmailAddress_AreDifferent_AsDocumented()
    {
        var email = UserIdentityClaim.Email.ToClaimUri();
        var emailAddress = UserIdentityClaim.EmailAddress.ToClaimUri();

        Assert.NotEqual(emailAddress, email);
        Assert.Equal(ClaimTypes.Email, emailAddress);
        Assert.StartsWith("http://schemas.microsoft.com/ws/2008/06/identity/claims/", email, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void All_Enum_Values_Have_Mappings()
    {
        foreach (UserIdentityClaim v in Enum.GetValues(typeof(UserIdentityClaim)))
        {
            var uri = v.ToClaimUri();
            Assert.False(string.IsNullOrWhiteSpace(uri));
        }
    }
}
