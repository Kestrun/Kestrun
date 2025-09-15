using Kestrun.Jwt;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtBuilderResultTests
{
#pragma warning disable IDE0004
    private static string NewB64Url(int bytes, byte value = 0xAB)
        => B64Url([.. Enumerable.Repeat(value, bytes).Select(b => (byte)b)]);
#pragma warning restore IDE0004
    private static string B64Url(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        s = s.Replace('+', '-').Replace('/', '_');
        return s.TrimEnd('=');
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public async Task BuildWithSymmetricKey_GeneratesValidToken_AndValidateAsyncPasses()
    {
        var b64 = NewB64Url(64); // 512-bit key ⇒ HS512 with Auto

        var builder = JwtTokenBuilder
            .New()
            .WithIssuer("https://issuer")
            .WithAudience("api://aud")
            .WithSubject("alice")
            .ValidFor(TimeSpan.FromMinutes(5))
            .SignWithSecret(b64, JwtAlgorithm.Auto);

        var result = builder.Build();

        // Token basics
        var jwt = result.Token();
        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.Contains('.', jwt);

        // Validation parameters reflect builder and key
        var tvp = result.GetValidationParameters();
        Assert.True(tvp.ValidateLifetime);
        Assert.Equal(TimeSpan.FromMinutes(1), tvp.ClockSkew);
        Assert.True(tvp.RequireSignedTokens);
        Assert.True(tvp.ValidateIssuerSigningKey);
        _ = Assert.IsType<SymmetricSecurityKey>(tvp.IssuerSigningKey);

        // Issuer/audience configured on builder are flowed
        Assert.True(tvp.ValidateIssuer);
        Assert.Equal("https://issuer", tvp.ValidIssuer);
        Assert.True(tvp.ValidateAudience);
        Assert.Equal("api://aud", tvp.ValidAudience);

        // The algorithm list should contain exactly the builder's algorithm
        _ = Assert.Single(tvp.ValidAlgorithms);
        Assert.Equal(builder.Algorithm, tvp.ValidAlgorithms.Single());

        // Round-trip validation succeeds
        var validation = await result.ValidateAsync(jwt);
        Assert.True(validation.IsValid);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void GetValidationParameters_Respects_CustomClockSkew()
    {
        var b64 = NewB64Url(48); // 384-bit key ⇒ HS384 with Auto
        var result = JwtTokenBuilder
            .New()
            .WithIssuer("iss")
            .WithAudience("aud")
            .SignWithSecret(b64, JwtAlgorithm.Auto)
            .Build();

        var skew = TimeSpan.FromSeconds(5);
        var tvp = result.GetValidationParameters(skew);
        Assert.Equal(skew, tvp.ClockSkew);
        Assert.True(tvp.ValidateLifetime);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public async Task ValidateAsync_Throws_When_Key_Is_Null()
    {
        // Construct a result with a null key (e.g., as produced by RSA signing)
        var dummy = new JwtBuilderResult(
            token: "x.y.z",
            key: null,
            builder: JwtTokenBuilder.New(),
            issuedAt: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(1));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => dummy.ValidateAsync("x.y.z"));

        // But GetValidationParameters should still be produced (no signing key)
        var tvp = dummy.GetValidationParameters();
        Assert.False(tvp.RequireSignedTokens);
        Assert.False(tvp.ValidateIssuerSigningKey);
        Assert.Null(tvp.IssuerSigningKey);
    }
}
