using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public class JwkUtilitiesTests
{
    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithValidParameters_ReturnsBase64UrlEncodedHash()
    {
        // Arrange - Use test vectors from RFC 7638 Section 3.1
        var n = "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw";
        var e = "AQAB";

        // Act
        var thumbprint = JwkUtilities.ComputeThumbprintRsa(n, e);

        // Assert
        Assert.NotNull(thumbprint);
        Assert.NotEmpty(thumbprint);
        // The thumbprint should be Base64Url encoded (no +, /, or = characters)
        Assert.DoesNotContain("+", thumbprint);
        Assert.DoesNotContain("/", thumbprint);
        Assert.DoesNotContain("=", thumbprint);

        // Expected thumbprint from RFC 7638 Section 3.1
        Assert.Equal("NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs", thumbprint);
    }

    [Theory]
    [InlineData(null, "AQAB")]
    [InlineData("", "AQAB")]
    [InlineData("  ", "AQAB")]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithNullOrEmptyModulus_ThrowsArgumentException(string? n, string e)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => JwkUtilities.ComputeThumbprintRsa(n!, e));
        Assert.Equal("nBase64Url", ex.ParamName);
    }

    [Theory]
    [InlineData("validN", null)]
    [InlineData("validN", "")]
    [InlineData("validN", "  ")]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithNullOrEmptyExponent_ThrowsArgumentException(string n, string? e)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => JwkUtilities.ComputeThumbprintRsa(n, e!));
        Assert.Equal("eBase64Url", ex.ParamName);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintFromCertificate_WithRsaCertificate_ReturnsValidThumbprint()
    {
        // Arrange - Create a test RSA certificate
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var thumbprint = JwkUtilities.ComputeThumbprintFromCertificate(cert);

        // Assert
        Assert.NotNull(thumbprint);
        Assert.NotEmpty(thumbprint);
        // Base64Url encoding validation
        Assert.DoesNotContain("+", thumbprint);
        Assert.DoesNotContain("/", thumbprint);
        Assert.DoesNotContain("=", thumbprint);

        // Verify it's at least 32 characters (SHA256 hash in Base64Url is 43 chars without padding)
        Assert.True(thumbprint.Length >= 32);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintFromCertificate_WithNullCertificate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => JwkUtilities.ComputeThumbprintFromCertificate(null!));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintFromCertificate_WithEcdsaCertificate_ThrowsNotSupportedException()
    {
        // Arrange - Create an ECDSA certificate (not RSA)
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=Test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => JwkUtilities.ComputeThumbprintFromCertificate(cert));
        Assert.Contains("RSA", ex.Message);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithSameParameters_ReturnsSameThumbprint()
    {
        // Arrange
        var n = "test_modulus_base64url";
        var e = "AQAB";

        // Act
        var thumbprint1 = JwkUtilities.ComputeThumbprintRsa(n, e);
        var thumbprint2 = JwkUtilities.ComputeThumbprintRsa(n, e);

        // Assert - Should be deterministic
        Assert.Equal(thumbprint1, thumbprint2);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithDifferentModulus_ReturnsDifferentThumbprint()
    {
        // Arrange
        var n1 = "modulus_one";
        var n2 = "modulus_two";
        var e = "AQAB";

        // Act
        var thumbprint1 = JwkUtilities.ComputeThumbprintRsa(n1, e);
        var thumbprint2 = JwkUtilities.ComputeThumbprintRsa(n2, e);

        // Assert
        Assert.NotEqual(thumbprint1, thumbprint2);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintRsa_WithDifferentExponent_ReturnsDifferentThumbprint()
    {
        // Arrange
        var n = "test_modulus";
        var e1 = "AQAB";
        var e2 = "AAEAAQ";

        // Act
        var thumbprint1 = JwkUtilities.ComputeThumbprintRsa(n, e1);
        var thumbprint2 = JwkUtilities.ComputeThumbprintRsa(n, e2);

        // Assert
        Assert.NotEqual(thumbprint1, thumbprint2);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ComputeThumbprintFromCertificate_UsesCanonicalJwkOrdering()
    {
        // Arrange - RFC 7638 specifies canonical ordering: e, kty, n
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var thumbprint1 = JwkUtilities.ComputeThumbprintFromCertificate(cert);

        // Get the same thumbprint using manual computation
        using var rsaPublic = cert.GetRSAPublicKey()!;
        var parameters = rsaPublic.ExportParameters(false);
        Assert.NotNull(parameters.Modulus);
        Assert.NotNull(parameters.Exponent);
        var n = Convert.ToBase64String(parameters.Modulus).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var e = Convert.ToBase64String(parameters.Exponent).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var thumbprint2 = JwkUtilities.ComputeThumbprintRsa(n, e);

        // Assert - Both methods should produce the same result
        Assert.Equal(thumbprint1, thumbprint2);
    }
}
