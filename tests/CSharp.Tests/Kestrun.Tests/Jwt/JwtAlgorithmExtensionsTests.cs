using Kestrun.Jwt;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrunTests.Jwt;

public class JwtAlgorithmExtensionsTests
{
    [Theory]
    [InlineData(JwtAlgorithm.HS256, SecurityAlgorithms.HmacSha256)]
    [InlineData(JwtAlgorithm.HS384, SecurityAlgorithms.HmacSha384)]
    [InlineData(JwtAlgorithm.HS512, SecurityAlgorithms.HmacSha512)]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithHmacAlgorithms_ReturnsCorrectSecurityAlgorithm(JwtAlgorithm alg, string expected)
    {
        // Act
        var result = alg.ToJwtString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(JwtAlgorithm.RS256, SecurityAlgorithms.RsaSha256)]
    [InlineData(JwtAlgorithm.RS384, SecurityAlgorithms.RsaSha384)]
    [InlineData(JwtAlgorithm.RS512, SecurityAlgorithms.RsaSha512)]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithRsaAlgorithms_ReturnsCorrectSecurityAlgorithm(JwtAlgorithm alg, string expected)
    {
        // Act
        var result = alg.ToJwtString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(JwtAlgorithm.PS256, SecurityAlgorithms.RsaSsaPssSha256)]
    [InlineData(JwtAlgorithm.PS384, SecurityAlgorithms.RsaSsaPssSha384)]
    [InlineData(JwtAlgorithm.PS512, SecurityAlgorithms.RsaSsaPssSha512)]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithRsaPssAlgorithms_ReturnsCorrectSecurityAlgorithm(JwtAlgorithm alg, string expected)
    {
        // Act
        var result = alg.ToJwtString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(JwtAlgorithm.ES256, SecurityAlgorithms.EcdsaSha256)]
    [InlineData(JwtAlgorithm.ES384, SecurityAlgorithms.EcdsaSha384)]
    [InlineData(JwtAlgorithm.ES512, SecurityAlgorithms.EcdsaSha512)]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithEcdsaAlgorithms_ReturnsCorrectSecurityAlgorithm(JwtAlgorithm alg, string expected)
    {
        // Act
        var result = alg.ToJwtString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(32, SecurityAlgorithms.HmacSha256)]  // 256 bits
    [InlineData(48, SecurityAlgorithms.HmacSha384)]  // 384 bits
    [InlineData(64, SecurityAlgorithms.HmacSha512)]  // 512 bits
    [InlineData(16, SecurityAlgorithms.HmacSha256)]  // Less than 256 bits defaults to HS256
    [InlineData(96, SecurityAlgorithms.HmacSha512)]  // More than 512 bits uses HS512
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithAutoAndKeyLength_ReturnsAppropriateHmacAlgorithm(int keyByteLength, string expected)
    {
        // Arrange
        var alg = JwtAlgorithm.Auto;

        // Act
        var result = alg.ToJwtString(keyByteLength);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithAutoAndNoKeyLength_DefaultsToHS256()
    {
        // Arrange
        var alg = JwtAlgorithm.Auto;

        // Act
        var result = alg.ToJwtString();

        // Assert
        Assert.Equal(SecurityAlgorithms.HmacSha256, result);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithInvalidAlgorithm_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidAlg = (JwtAlgorithm)999;

        // Act & Assert
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => invalidAlg.ToJwtString());
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ToJwtString_WithAutoAndBoundaryKeyLength_ReturnsCorrectAlgorithm()
    {
        // Arrange
        var alg = JwtAlgorithm.Auto;

        // Act & Assert
        // Exactly 48 bytes should use HS384
        Assert.Equal(SecurityAlgorithms.HmacSha384, alg.ToJwtString(48));

        // 47 bytes should use HS256
        Assert.Equal(SecurityAlgorithms.HmacSha256, alg.ToJwtString(47));

        // Exactly 64 bytes should use HS512
        Assert.Equal(SecurityAlgorithms.HmacSha512, alg.ToJwtString(64));

        // 63 bytes should use HS384
        Assert.Equal(SecurityAlgorithms.HmacSha384, alg.ToJwtString(63));
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ToJwtString_MultipleCallsWithSameInput_ReturnsSameResult()
    {
        // Arrange
        var alg = JwtAlgorithm.RS256;

        // Act
        var result1 = alg.ToJwtString();
        var result2 = alg.ToJwtString();

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    [Trait("Category", "Jwt")]
    public void ToJwtString_AllDefinedAlgorithms_DoNotThrow()
    {
        // Arrange - Get all defined JwtAlgorithm values
        var allAlgorithms = Enum.GetValues<JwtAlgorithm>();

        // Act & Assert - All should convert without throwing
        foreach (var result in allAlgorithms.Select(alg => alg.ToJwtString(32)))
        {
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

    }
}
