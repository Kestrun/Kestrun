using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Certificates;
using Xunit;

namespace KestrunTests.Certificates;

public sealed class ClientCertificateValidationCallbacksTests
{
    private static X509Certificate2 CreateSelfSignedCertificate(string subject = "CN=Test")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public void AllowAny_ReturnsFalse_WhenCertificateIsNull()
    {
        var result = ClientCertificateValidationCallbacks.AllowAny(null!, new X509Chain(), SslPolicyErrors.None);
        Assert.False(result);
    }

    [Fact]
    public void AllowAny_ReturnsTrue_WhenCertificateIsPresent()
    {
        var result = ClientCertificateValidationCallbacks.AllowAny(CreateSelfSignedCertificate(), new X509Chain(), SslPolicyErrors.None);
        Assert.True(result);
    }

    [Fact]
    public void AllowSelfSignedForDevelopment_ReturnsFalse_WhenCertificateIsNull()
    {
        var result = ClientCertificateValidationCallbacks.AllowSelfSignedForDevelopment(null!, new X509Chain(), SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.False(result);
    }

    [Theory]
    [InlineData(SslPolicyErrors.None, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    public void AllowSelfSignedForDevelopment_OnlyAllowsNoneOrChainErrors(SslPolicyErrors errors, bool expected)
    {
        var result = ClientCertificateValidationCallbacks.AllowSelfSignedForDevelopment(CreateSelfSignedCertificate(), new X509Chain(), errors);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AllowMissingOrSelfSignedForDevelopment_AllowsMissingCertificate()
    {
        var result = ClientCertificateValidationCallbacks.AllowMissingOrSelfSignedForDevelopment(null!, new X509Chain(), SslPolicyErrors.None);
        Assert.True(result);
    }

    [Theory]
    [InlineData(SslPolicyErrors.None, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    public void AllowMissingOrSelfSignedForDevelopment_OnlyAllowsNoneOrChainErrors_WhenCertPresent(SslPolicyErrors errors, bool expected)
    {
        var result = ClientCertificateValidationCallbacks.AllowMissingOrSelfSignedForDevelopment(CreateSelfSignedCertificate(), new X509Chain(), errors);
        Assert.Equal(expected, result);
    }
}
