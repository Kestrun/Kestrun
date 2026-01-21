using Kestrun.Authentication;
using Xunit;

namespace KestrunTests.Authentication;

public sealed class AuthenticationTypeTests
{
    [Fact]
    public void AuthenticationType_IncludesCertificate()
    {
        Assert.True(Enum.IsDefined(typeof(AuthenticationType), AuthenticationType.Certificate));
    }
}
