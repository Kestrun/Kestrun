using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public partial class JwtTokenBuilderTests
{
    [Theory]
    [InlineData(32, "HS256")] // 256-bit
    [InlineData(48, "HS384")] // 384-bit
    [InlineData(64, "HS512")] // 512-bit
    public void SignWithSecret_Auto_Picks_Expected_Alg(int bytes, string expectedAlg)
    {
        var secret = B64Url([.. Enumerable.Repeat((byte)0x01, bytes)]);
        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("s").SignWithSecret(secret);
        var res = b.Build();
        Assert.Equal(expectedAlg, b.Algorithm);
        Assert.Contains(expectedAlg, res.GetValidationParameters().ValidAlgorithms);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithJwkJson_oct_Auto_Uses_HS256()
    {
        var key = Enumerable.Repeat((byte)0xAA, 32).ToArray();
        var b64u = B64Url(key);
        var jwk = $"{{\"kty\":\"oct\",\"k\":\"{b64u}\"}}";

        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("sub").SignWithJwkJson(jwk);
        var res = b.Build();
        Assert.Equal("HS256", b.Algorithm);
        Assert.False(string.IsNullOrWhiteSpace(res.Token()));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithJwkJson_oct_Incompatible_Alg_Throws()
    {
        var key = Enumerable.Repeat((byte)0xBB, 32).ToArray();
        var b64u = B64Url(key);
        var jwk = $"{{\"kty\":\"oct\",\"k\":\"{b64u}\"}}";

        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("sub").SignWithJwkJson(jwk, JwtAlgorithm.RS256);
        _ = Assert.Throws<NotSupportedException>(b.Build);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithJwkJson_RSA_Incompatible_Alg_Throws()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(false);
        var n = B64Url(p.Modulus!);
        var e = B64Url(p.Exponent!);
        var jwkRsa = $"{{\"kty\":\"RSA\",\"n\":\"{n}\",\"e\":\"{e}\"}}";

        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("sub").SignWithJwkJson(jwkRsa, JwtAlgorithm.HS256);
        _ = Assert.Throws<NotSupportedException>(b.Build);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void EncryptWithJwkJson_RSA_Public_Builds_Token()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(false);
        var n = B64Url(p.Modulus!);
        var e = B64Url(p.Exponent!);
        var jwkPub = $"{{\"kty\":\"RSA\",\"n\":\"{n}\",\"e\":\"{e}\"}}";

        var sign = B64Url([.. Enumerable.Repeat((byte)0xCC, 32)]);
        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("sub").SignWithSecret(sign).EncryptWithJwkJson(jwkPub, keyAlg: "RSA-OAEP", encAlg: "A256CBC-HS512");
        var token = b.Build().Token();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void AddHeader_Appears_In_Token_Header()
    {
        var secret = B64Url([.. Enumerable.Repeat((byte)0x02, 32)]);
        var token = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .AddHeader("cty", "JWT")
            .SignWithSecret(secret)
            .Build()
            .Token();

        var p = JwtInspector.ReadAllParameters(token);
        Assert.Equal("JWT", p.Header["cty"]);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void EncryptWithSecret_dir_CBC_Builds_Token()
    {
        var sign = B64Url([.. Enumerable.Repeat((byte)0x03, 32)]); // HS256 signing
        var encKey = Enumerable.Repeat((byte)0x04, 32).ToArray();       // 256-bit key for A128CBC-HS256

        var token = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .SignWithSecret(sign)
            .EncryptWithSecret(encKey, keyAlg: "dir", encAlg: "A128CBC-HS256")
            .Build()
            .Token();

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void EncryptWithSecret_dir_GCM_Builds_Token_IfSupported()
    {
        var sign = B64Url([.. Enumerable.Repeat((byte)0x0A, 32)]); // HS256 signing
        var encKey = Enumerable.Repeat((byte)0x0B, 16).ToArray();       // 128-bit key for A128GCM

        var sym = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(encKey);
        var cpf = Microsoft.IdentityModel.Tokens.CryptoProviderFactory.Default;
        // Pre-flight attempt: try creating an encryption provider and encrypting one byte
        try
        {
            var provider = cpf.CreateAuthenticatedEncryptionProvider(sym, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.Aes128Gcm);
            // Some implementations need only plaintext & AAD
            _ = provider.Encrypt([0x01], []);
        }
        catch (NotSupportedException)
        {
            // AES-GCM not available; skip
            return;
        }
        catch
        {
            // Ignore other preflight errors and try full token path
        }

        try
        {
            var token = JwtTokenBuilder.New()
                .WithIssuer("iss")
                .WithAudience("aud")
                .WithSubject("sub")
                .SignWithSecret(sign)
                .EncryptWithSecret(encKey, keyAlg: "dir", encAlg: "A128GCM")
                .Build()
                .Token();

            Assert.False(string.IsNullOrWhiteSpace(token));
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenEncryptionFailedException ex) when (ex.InnerException is NotSupportedException || (ex.InnerException?.Message?.Contains("IDX10715") ?? false))
        {
            // Final gate: runtime signaled unsupported GCM; treat as skip
            return;
        }
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void EncryptWithSecret_KeyWrapWrongSize_Throws()
    {
        var sign = B64Url([.. Enumerable.Repeat((byte)0x05, 32)]);
        var encKey = Enumerable.Repeat((byte)0x06, 16).ToArray(); // 128-bit but A256GCM requires 256

        var b = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .SignWithSecret(sign)
            .EncryptWithSecret(encKey, keyAlg: "A256KW", encAlg: "A256GCM");

#pragma warning disable IDE0200
        // Expect an exception due to key size mismatch
        _ = Assert.Throws<ArgumentException>(() => b.Build());
#pragma warning restore IDE0200
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithCertificate_ECDSA_ES256_Algorithm()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var cert = CreateEcdsaSelfSignedCertOrNull("CN=ES256", ecdsa) ?? throw new InvalidOperationException("Unable to create ECDSA certificate for ES256 test.");
        var b = JwtTokenBuilder.New().WithIssuer("iss").WithAudience("aud").WithSubject("s").SignWithCertificate(cert);
        var res = b.Build();
        Assert.Equal("ES256", b.Algorithm);
        Assert.Contains("ES256", res.GetValidationParameters().ValidAlgorithms);
        cert.Dispose();
    }

    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }

    private static X509Certificate2? CreateEcdsaSelfSignedCertOrNull(string subject, ECDsa ecdsa)
    {
        // Use reflection to avoid compile-time dependency on CertificateRequest
        var typeName = "System.Security.Cryptography.X509Certificates.CertificateRequest, System.Security.Cryptography.X509Certificates";
        var t = Type.GetType(typeName, throwOnError: false);
        if (t == null)
        {
            return null;
        }

        var ctor = t.GetConstructors()
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(ECDsa) && ps[2].ParameterType == typeof(HashAlgorithmName);
            });
        if (ctor == null)
        {
            return null;
        }

        var req = ctor.Invoke([subject, ecdsa, HashAlgorithmName.SHA256]);
        var mi = t.GetMethod("CreateSelfSigned", [typeof(DateTimeOffset), typeof(DateTimeOffset)]);
        if (mi == null)
        {
            return null;
        }
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddMinutes(10);
        return (X509Certificate2?)mi.Invoke(req, [notBefore, notAfter]);
    }
}
