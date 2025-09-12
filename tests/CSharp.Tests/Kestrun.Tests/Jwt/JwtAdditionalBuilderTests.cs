using System.Security;
using System.Security.Cryptography;
using Kestrun.Jwt;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtAdditionalBuilderTests
{
    [Fact]
    [Trait("Category", "Jwt")]
    public void SignWithSecretPassphrase_32Bytes_Uses_HS256()
    {
        using var pass = new SecureString();
        for (var i = 0; i < 32; i++)
        {
            pass.AppendChar('a');
        }

        pass.MakeReadOnly();

        var b = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("sub")
            .SignWithSecretPassphrase(pass);

        var res = b.Build();
        Assert.Equal("HS256", b.Algorithm);
        Assert.False(string.IsNullOrWhiteSpace(res.Token()));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void EncryptWithPemPublic_RSAOAEP_CBC_Builds_Token()
    {
        using var rsa = RSA.Create(2048);
        var pubPem = ExportPublicKeyPem(rsa);
        var pemPath = CreateTempPemFile(pubPem);

        try
        {
            var sign = B64Url([.. Enumerable.Repeat((byte)0x11, 32)]);
            var token = JwtTokenBuilder.New()
                .WithIssuer("iss")
                .WithAudience("aud")
                .WithSubject("sub")
                .SignWithSecret(sign)
                .EncryptWithPemPublic(pemPath, keyAlg: "RSA-OAEP", encAlg: "A128CBC-HS256")
                .Build()
                .Token();

            Assert.False(string.IsNullOrWhiteSpace(token));
        }
        finally
        {
            try { File.Delete(pemPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public async Task RenewJwt_FromContext_ParsesBearerAndRenews()
    {
        var sign = B64Url([.. Enumerable.Repeat((byte)0x22, 32)]);
        var b = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("alice")
            .ValidFor(TimeSpan.FromMinutes(1))
            .SignWithSecret(sign);

        var original = b.Build().Token();

        var http = new DefaultHttpContext();
        http.Request.Headers["Authorization"] = "Bearer " + original;
        var req = await KestrunRequest.NewRequest(http);
        var resp = new KestrunResponse(req);
        var ctx = new KestrunContext(req, resp, http);

        var renewed = b.RenewJwt(ctx, TimeSpan.FromMinutes(2));
        Assert.False(string.IsNullOrWhiteSpace(renewed));

        var p1 = JwtInspector.ReadAllParameters(original);
        var p2 = JwtInspector.ReadAllParameters(renewed);
        Assert.True(p2.Expires > p1.Expires);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public async Task RenewJwt_FromContext_NoBearer_ReturnsEmpty()
    {
        var sign = B64Url([.. Enumerable.Repeat((byte)0x33, 32)]);
        var b = JwtTokenBuilder.New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("bob")
            .SignWithSecret(sign);

        var http = new DefaultHttpContext();
        var req = await KestrunRequest.NewRequest(http);
        var resp = new KestrunResponse(req);
        var ctx = new KestrunContext(req, resp, http);

        var renewed = b.RenewJwt(ctx, TimeSpan.FromMinutes(1));
        Assert.Equal(string.Empty, renewed);
    }

    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }

    private static string CreateTempPemFile(string pem)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, pem);
        return path;
    }

    private static string ExportPublicKeyPem(RSA rsa)
    {
        var builder = new System.Text.StringBuilder();
        _ = builder.AppendLine("-----BEGIN PUBLIC KEY-----");
        var export = rsa.ExportSubjectPublicKeyInfo();
        _ = builder.AppendLine(Convert.ToBase64String(export, Base64FormattingOptions.InsertLineBreaks));
        _ = builder.AppendLine("-----END PUBLIC KEY-----");
        return builder.ToString();
    }
}
