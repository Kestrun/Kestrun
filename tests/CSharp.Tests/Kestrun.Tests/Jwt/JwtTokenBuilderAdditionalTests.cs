using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Kestrun.Jwt;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtTokenBuilderAdditionalTests
{
    private static string MakeHex(int bytes, byte value = 0x11)
        => Convert.ToHexString([.. Enumerable.Repeat(value, bytes).Select(b => b)]);

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithSecretHex_Produces_Expected_Hmac_Size()
    {
        // 64 bytes (128 hex chars) ⇒ HS512 when Auto
        var hex = MakeHex(64);
        var builder = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("bob")
            .SignWithSecretHex(hex, JwtAlgorithm.Auto);

        var token = builder.Build().Token();
        var headerAlg = new JwtSecurityTokenHandler().ReadJwtToken(token).Header.Alg;
        Assert.Equal(SecurityAlgorithms.HmacSha512, headerAlg);
        Assert.Equal(SecurityAlgorithms.HmacSha512, builder.Algorithm);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithSecret_Empty_Throws()
    {
        var builder = JwtTokenBuilder.New();
        _ = Assert.Throws<ArgumentNullException>(() => builder.SignWithSecret(""));
        _ = Assert.Throws<ArgumentNullException>(() => builder.SignWithSecret("   "));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void NotBefore_In_Past_Is_Clamped_On_Build()
    {
        var hex = MakeHex(32); // 256-bit ⇒ HS256
        var past = DateTime.UtcNow.AddHours(-2);
        var result = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("alice")
            .NotBefore(past)
            .ValidFor(TimeSpan.FromMinutes(10))
            .SignWithSecretHex(hex)
            .Build();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token());
        // The builder clamps _nbf to now if earlier; allow small skew
        Assert.True((DateTime.UtcNow - jwt.ValidFrom.ToUniversalTime()) < TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithCertificate_Auto_Picks_RS256_For_Rsa()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=RSATest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        var token = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("rsa-user")
            .SignWithCertificate(cert, JwtAlgorithm.Auto)
            .Build().Token();
        var header = new JwtSecurityTokenHandler().ReadJwtToken(token).Header;
        Assert.Equal(SecurityAlgorithms.RsaSha256, header.Alg);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithCertificate_NoPrivateKey_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=NoPrivKey", ecdsa, HashAlgorithmName.SHA256);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        // Export public only to drop private key
#if NET9_0
        var publicOnly = X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Cert));
#else
        var publicOnly = new X509Certificate2(cert.Export(X509ContentType.Cert));
#endif
        var builder = JwtTokenBuilder.New();
        _ = Assert.Throws<ArgumentException>(() => builder.SignWithCertificate(publicOnly));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithRsaPem_Auto_Uses_RS256()
    {
        using var rsa = RSA.Create(2048);
        var pem = ExportPrivatePem(rsa);
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, pem);
        try
        {
            var token = JwtTokenBuilder.New()
                .WithIssuer("iss")
                .WithAudience("aud")
                .WithSubject("pem-user")
                .SignWithRsaPem(tmp, JwtAlgorithm.Auto)
                .Build().Token();
            var header = new JwtSecurityTokenHandler().ReadJwtToken(token).Header;
            Assert.Equal(SecurityAlgorithms.RsaSha256, header.Alg);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    private static string ExportPrivatePem(RSA rsa)
    {
        var builder = new StringBuilder();
        _ = builder.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        _ = builder.AppendLine(Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks));
        _ = builder.AppendLine("-----END RSA PRIVATE KEY-----");
        return builder.ToString();
    }
}
