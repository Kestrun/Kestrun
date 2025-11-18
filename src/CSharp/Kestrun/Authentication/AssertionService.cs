using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Authentication;
/// <summary>
/// Service to create OpenID Connect client assertions.
/// </summary>
public class AssertionService
{
    private readonly JsonWebKey _jwk;
    private readonly SigningCredentials _credentials;
    private readonly string _clientId;

    /// <summary>
    /// Gets the Client ID.
    /// </summary>
    public string ClientId => _clientId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionService"/> class.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="jwkJson"></param>
    public AssertionService(string clientId, string? jwkJson)
    {
        _clientId = clientId;
        _jwk = new JsonWebKey(jwkJson);
        _credentials = new SigningCredentials(_jwk, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Creates a client assertion for the specified token endpoint.
    /// </summary>
    /// <param name="tokenEndpoint"> The token endpoint for which the assertion is created.</param>
    /// <returns></returns>
    public string CreateClientAssertion(string tokenEndpoint)
    {
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new("iss", _clientId),
            new("sub", _clientId),
            new(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: _clientId,
            audience: tokenEndpoint,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(1),
            signingCredentials: _credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
