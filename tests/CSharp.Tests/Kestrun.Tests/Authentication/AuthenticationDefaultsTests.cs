using Kestrun.Authentication;
using Xunit;

namespace KestrunTests.Authentication;

public sealed class AuthenticationDefaultsTests
{
    [Fact]
    public void CertificateDefaults_AreStable()
    {
        Assert.Equal("Certificate", AuthenticationDefaults.CertificateSchemeName);
        Assert.Equal("Client Certificate Authentication", AuthenticationDefaults.CertificateDisplayName);
    }
}
