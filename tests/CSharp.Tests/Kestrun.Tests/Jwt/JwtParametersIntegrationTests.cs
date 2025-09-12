using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtParametersIntegrationTests
{
    [Fact]
    [Trait("Category", "Jwt")]
    public void HS256_Parameters_RoundTrip_WithHeaders()
    {
        var secret = B64Url([.. Enumerable.Repeat((byte)0xA5, 32)]);
        var b = JwtTokenBuilder.New()
            .WithIssuer("iss-hs")
            .WithAudience("aud-hs")
            .WithSubject("sub-hs")
            .AddClaim("scope", "write")
            .AddHeader("cty", "JWT")
            .ValidFor(TimeSpan.FromMinutes(2))
            .SignWithSecret(secret);

        var res = b.Build();
        var p = JwtInspector.ReadAllParameters(res.Token());

        Assert.Equal("iss-hs", p.Issuer);
        Assert.Contains("aud-hs", p.Audiences);
        Assert.Equal("sub-hs", p.Subject);
        _ = Assert.NotNull(p.NotBefore);
        _ = Assert.NotNull(p.IssuedAt);
        _ = Assert.NotNull(p.Expires);
        Assert.Equal("JWT", p.Type);
        Assert.False(string.IsNullOrWhiteSpace(p.Algorithm));
        Assert.False(string.IsNullOrWhiteSpace(p.KeyId));
        Assert.Equal("write", p.Claims["scope"]);
        Assert.Contains("cty", p.Header.Keys);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void RSA_Parameters_RoundTrip_WithHeaders()
    {
        using var rsa = RSA.Create(2048);
        var pem = ExportPrivateKeyPem(rsa);
        var pemPath = CreateTempPemFile(pem);

        var b = JwtTokenBuilder.New()
            .WithIssuer("iss-rsa")
            .WithAudience("aud-rsa")
            .WithSubject("sub-rsa")
            .AddClaim("ten", "42")
            .AddHeader("x-test", "yes")
            .SignWithRsaPem(pemPath);

        var res = b.Build();
        var p = JwtInspector.ReadAllParameters(res.Token());

        Assert.Equal("iss-rsa", p.Issuer);
        Assert.Contains("aud-rsa", p.Audiences);
        Assert.Equal("sub-rsa", p.Subject);
        Assert.Equal("42", p.Claims["ten"]);
        Assert.Equal("JWT", p.Type);
        Assert.False(string.IsNullOrWhiteSpace(p.Algorithm));
        Assert.False(string.IsNullOrWhiteSpace(p.KeyId));
        Assert.Contains("x-test", p.Header.Keys);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Cert_Parameters_RoundTrip_WithHeaders()
    {
        using var cert = CreateSelfSignedRsaCert();
        var b = JwtTokenBuilder.New()
            .WithIssuer("iss-cert")
            .WithAudience("aud-cert")
            .WithSubject("sub-cert")
            .AddClaim("role", "reader")
            .AddHeader("kid-hint", "present")
            .SignWithCertificate(cert);

        var res = b.Build();
        var p = JwtInspector.ReadAllParameters(res.Token());

        Assert.Equal("iss-cert", p.Issuer);
        Assert.Contains("aud-cert", p.Audiences);
        Assert.Equal("sub-cert", p.Subject);
        Assert.Equal("reader", p.Claims["role"]);
        Assert.Equal("JWT", p.Type);
        Assert.False(string.IsNullOrWhiteSpace(p.Algorithm));
        Assert.False(string.IsNullOrWhiteSpace(p.KeyId)); // thumbprint-backed kid
        Assert.Contains("kid-hint", p.Header.Keys);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Ecdsa_Parameters_RoundTrip_ES256()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=ParamsES256", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(10));
        // ES256 capability is enabled via a custom CryptoProviderFactory in tests

        var b = JwtTokenBuilder.New()
            .WithIssuer("iss-es")
            .WithAudience("aud-es")
            .WithSubject("sub-es")
            .AddClaim("perm", "list")
            .SignWithCertificate(cert);

        var res = b.Build();
        var p = JwtInspector.ReadAllParameters(res.Token());

        Assert.Equal("iss-es", p.Issuer);
        Assert.Contains("aud-es", p.Audiences);
        Assert.Equal("sub-es", p.Subject);
        Assert.Equal("list", p.Claims["perm"]);
        Assert.Equal("JWT", p.Type);
        Assert.False(string.IsNullOrWhiteSpace(p.Algorithm));
        Assert.False(string.IsNullOrWhiteSpace(p.KeyId));
    }

    // helpers
    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
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

    private static string CreateTempPemFile(string pem)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, pem);
        return path;
    }

    private static X509Certificate2 CreateSelfSignedRsaCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=ParamsCert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(10));
    }
}
