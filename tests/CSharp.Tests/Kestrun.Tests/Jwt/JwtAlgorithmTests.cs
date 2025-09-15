using Kestrun.Jwt;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtAlgorithmTests
{
    [Theory]
    [InlineData(0, SecurityAlgorithms.HmacSha256)]    // default/unknown size ⇒ HS256
    [InlineData(16, SecurityAlgorithms.HmacSha256)]   // < 48 ⇒ HS256
    [InlineData(47, SecurityAlgorithms.HmacSha256)]
    [InlineData(48, SecurityAlgorithms.HmacSha384)]   // >= 48 and < 64 ⇒ HS384
    [InlineData(63, SecurityAlgorithms.HmacSha384)]
    [InlineData(64, SecurityAlgorithms.HmacSha512)]   // >= 64 ⇒ HS512
    [InlineData(100, SecurityAlgorithms.HmacSha512)]
    public void Auto_Chooses_Hmac_By_KeyLength(int keyBytes, string expectedAlg)
    {
        var alg = JwtAlgorithm.Auto.ToJwtString(keyBytes);
        Assert.Equal(expectedAlg, alg);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Hmac_Mappings_Are_Correct()
    {
        Assert.Equal(SecurityAlgorithms.HmacSha256, JwtAlgorithm.HS256.ToJwtString());
        Assert.Equal(SecurityAlgorithms.HmacSha384, JwtAlgorithm.HS384.ToJwtString());
        Assert.Equal(SecurityAlgorithms.HmacSha512, JwtAlgorithm.HS512.ToJwtString());
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Rsa_Mappings_Are_Correct()
    {
        Assert.Equal(SecurityAlgorithms.RsaSha256, JwtAlgorithm.RS256.ToJwtString());
        Assert.Equal(SecurityAlgorithms.RsaSha384, JwtAlgorithm.RS384.ToJwtString());
        Assert.Equal(SecurityAlgorithms.RsaSha512, JwtAlgorithm.RS512.ToJwtString());
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void RsaPss_Mappings_Are_Correct()
    {
        Assert.Equal(SecurityAlgorithms.RsaSsaPssSha256, JwtAlgorithm.PS256.ToJwtString());
        Assert.Equal(SecurityAlgorithms.RsaSsaPssSha384, JwtAlgorithm.PS384.ToJwtString());
        Assert.Equal(SecurityAlgorithms.RsaSsaPssSha512, JwtAlgorithm.PS512.ToJwtString());
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Ecdsa_Mappings_Are_Correct()
    {
        Assert.Equal(SecurityAlgorithms.EcdsaSha256, JwtAlgorithm.ES256.ToJwtString());
        Assert.Equal(SecurityAlgorithms.EcdsaSha384, JwtAlgorithm.ES384.ToJwtString());
        Assert.Equal(SecurityAlgorithms.EcdsaSha512, JwtAlgorithm.ES512.ToJwtString());
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void Unknown_Enum_Value_Throws()
    {
        var invalid = (JwtAlgorithm)9999;
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => invalid.ToJwtString());
    }
}
