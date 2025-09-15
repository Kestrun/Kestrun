using Kestrun.Jwt;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtRenewalTests
{
    private static string NewSecretB64u(int bytes)
        => B64Url([.. Enumerable.Repeat((byte)0xCD, bytes)]);

    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void RenewJwt_PreservesClaimsAndUpdatesTimes()
    {
        var secret = NewSecretB64u(32); // 256-bit key (HS256)

        var builder = JwtTokenBuilder
            .New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .WithSubject("carol")
            .AddClaim("role", "admin")
            .ValidFor(TimeSpan.FromMinutes(1))
            .SignWithSecret(secret);

        var result = builder.Build();
        var jwt = result.Token();

        // Renew for a longer lifetime
        var renewed = builder.RenewJwt(jwt, TimeSpan.FromMinutes(2));

        // Inspect both tokens
        var p1 = JwtInspector.ReadAllParameters(jwt);
        var p2 = JwtInspector.ReadAllParameters(renewed);

        Assert.Equal(p1.Issuer, p2.Issuer);
        // Some handlers may surface duplicate 'aud' when rebuilt; compare distinct sets
        var a1 = p1.Audiences.Distinct().OrderBy(x => x).ToArray();
        var a2 = p2.Audiences.Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(a1, a2);
        Assert.Equal(p1.Subject, p2.Subject);
        Assert.Equal(p1.Claims["role"], p2.Claims["role"]);

        Assert.True(p2.NotBefore >= p1.NotBefore);
        Assert.True(p2.Expires > p1.Expires);
    }
}
