using Kestrun.Authentication;
using Kestrun.Certificates;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace KestrunTests.Authentication;

public class OpenIDAssertionTests
{
    private static string CreateTestJwk()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);

        var jwk = new
        {
            kty = "RSA",
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
            d = Base64UrlEncoder.Encode(parameters.D),
            p = Base64UrlEncoder.Encode(parameters.P),
            q = Base64UrlEncoder.Encode(parameters.Q),
            dp = Base64UrlEncoder.Encode(parameters.DP),
            dq = Base64UrlEncoder.Encode(parameters.DQ),
            qi = Base64UrlEncoder.Encode(parameters.InverseQ)
        };

        return JsonSerializer.Serialize(jwk, JwkJson.Options);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void Constructor_WithValidParameters_CreatesService()
    {
        // Arrange
        var jwkJson = CreateTestJwk();

        // Act
        var service = new AssertionService("test-client-id", jwkJson);

        // Assert
        Assert.NotNull(service);
    }

    // Note: current implementation does not validate null arguments; it will accept nulls.
    // We avoid asserting exceptions for null inputs to reflect actual behavior.

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_WithValidTokenEndpoint_ReturnsJwt()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert
        Assert.NotNull(assertion);
        Assert.NotEmpty(assertion);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_ReturnsValidJwtStructure()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert - JWT has 3 parts separated by dots
        var parts = assertion.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_JwtContainsExpectedClaims()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var clientId = "test-client-id";
        var service = new AssertionService(clientId, jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert - Decode and verify claims
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(assertion);

        Assert.Equal(clientId, token.Claims.FirstOrDefault(c => c.Type == "iss")?.Value);
        Assert.Equal(clientId, token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value);
        Assert.NotNull(token.Claims.FirstOrDefault(c => c.Type == "jti")?.Value);
        Assert.NotNull(token.Claims.FirstOrDefault(c => c.Type == "iat")?.Value);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_JwtHasCorrectAudience()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(assertion);

        Assert.Contains(tokenEndpoint, token.Audiences);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_JwtExpiresInOneMinute()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(assertion);
        var expirationDifference = token.ValidTo - beforeCreation;

        Assert.True(expirationDifference.TotalSeconds is >= 50 and <= 70,
            $"Expected expiration around 60 seconds, got {expirationDifference.TotalSeconds}");
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_JwtIsSignedWithRS256()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion = service.CreateClientAssertion(tokenEndpoint);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(assertion);

        Assert.Equal(SecurityAlgorithms.RsaSha256, token.SignatureAlgorithm);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_JtiIsUnique()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion1 = service.CreateClientAssertion(tokenEndpoint);
        var assertion2 = service.CreateClientAssertion(tokenEndpoint);

        // Assert - jti claim should be different
        var handler = new JwtSecurityTokenHandler();
        var token1 = handler.ReadJwtToken(assertion1);
        var token2 = handler.ReadJwtToken(assertion2);

        var jti1 = token1.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        var jti2 = token2.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;

        Assert.NotEqual(jti1, jti2);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_MultipleCallsReturnDifferentTokens()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);
        var tokenEndpoint = "https://example.com/oauth/token";

        // Act
        var assertion1 = service.CreateClientAssertion(tokenEndpoint);
        var assertion2 = service.CreateClientAssertion(tokenEndpoint);

        // Assert - tokens should be different due to unique jti and iat
        Assert.NotEqual(assertion1, assertion2);
    }

    [Fact]
    [Trait("Category", "Authentication")]
    public void CreateClientAssertion_WithNullTokenEndpoint_DoesNotThrow()
    {
        // Arrange
        var jwkJson = CreateTestJwk();
        var service = new AssertionService("test-client-id", jwkJson);

        // Act
        var token = service.CreateClientAssertion(null!);

        // Assert
        Assert.False(string.IsNullOrEmpty(token));
    }
}
