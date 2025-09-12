using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtBuilderResultRsaCertTests
{
    private static X509Certificate2 CreateSelfSignedRsaCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=TestCert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        // CreateSelfSigned with RSA on Windows yields a cert with private key already associated
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(10));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public async Task SignWithRsaPem_ValidateAsync_Succeeds_AndNoValidAlgorithmsEnforced()
    {
        using var rsa = RSA.Create(2048);
        var pem = ExportPrivateKeyPem(rsa);

        var result = JwtTokenBuilder
            .New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("bob")
            .ValidFor(TimeSpan.FromMinutes(2))
            .SignWithRsaPem(CreateTempPemFile(pem))
            .Build();

        var token = result.Token();
        Assert.False(string.IsNullOrWhiteSpace(token));

        // With RSA, JwtBuilderResult stores key as null; TVP should not require signature key
        var tvp = result.GetValidationParameters();
        Assert.False(tvp.RequireSignedTokens);
        Assert.False(tvp.ValidateIssuerSigningKey);
        Assert.Null(tvp.IssuerSigningKey);

        // For RSA, builder.Algorithm resolves to an RS* alg; TVP.ValidAlgorithms should reflect that
        Assert.Contains("RS256", tvp.ValidAlgorithms);

        // ValidateAsync should throw since key is null (by design)
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => result.ValidateAsync(token));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithCertificate_TVPReflects_NoSigningKey_AndEmptyAlgorithms()
    {
        using var cert = CreateSelfSignedRsaCert();
        var result = JwtTokenBuilder
            .New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("eve")
            .SignWithCertificate(cert)
            .Build();

        var tvp = result.GetValidationParameters();
        Assert.False(tvp.RequireSignedTokens);
        Assert.False(tvp.ValidateIssuerSigningKey);
        Assert.Null(tvp.IssuerSigningKey);
        Assert.Contains("RS256", tvp.ValidAlgorithms);
    }

    private static string CreateTempPemFile(string pem)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, pem);
        return path;
    }

    private static string ExportPrivateKeyPem(RSA rsa)
    {
        var builder = new System.Text.StringBuilder();
        _ = builder.AppendLine("-----BEGIN PRIVATE KEY-----");
        var export = rsa.ExportPkcs8PrivateKey();
        _ = builder.AppendLine(Convert.ToBase64String(export, Base64FormattingOptions.InsertLineBreaks));
        _ = builder.AppendLine("-----END PRIVATE KEY-----");
        return builder.ToString();
    }
}
