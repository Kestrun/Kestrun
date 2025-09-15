using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Kestrun.Jwt;

/// <summary>
/// Represents the result of building a JWT, including the token, key, builder, issue time, and expiration.
/// </summary>
/// <param name="token">The JWT compact string.</param>
/// <param name="key">The symmetric security key used for signing.</param>
/// <param name="builder">The JWT token builder instance.</param>
/// <param name="issuedAt">The time at which the token was issued.</param>
/// <param name="expires">The expiration time of the token.</param>
public sealed class JwtBuilderResult(
    string token,
    SymmetricSecurityKey? key,
    JwtTokenBuilder builder,
    DateTime issuedAt,
    DateTime expires)
{
    private readonly string _token = token;
    private readonly SymmetricSecurityKey? _key = key;
    private readonly JwtTokenBuilder _builder = builder;

    /// <summary>
    /// Gets the time at which the token was issued.
    /// </summary>
    public DateTime IssuedAt { get; } = issuedAt;
    /// <summary>
    /// Gets the expiration time of the token.
    /// </summary>
    public DateTime Expires { get; private set; } = expires;

    /// <summary>Get the JWT compact string.</summary>
    public string Token() => _token;


    /// <summary>
    /// Gets the <see cref="TokenValidationParameters"/> for validating the JWT token.
    /// </summary>
    /// <param name="clockSkew">Optional clock skew to allow when validating token lifetime.</param>
    /// <returns>The configured <see cref="TokenValidationParameters"/> instance.</returns>
    public TokenValidationParameters GetValidationParameters(TimeSpan? clockSkew = null)
    {
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = _builder.Issuer is not null,
            ValidIssuer = _builder.Issuer,
            ValidateAudience = _builder.Audience is not null,
            ValidAudience = _builder.Audience,
            ValidateLifetime = true,
            ClockSkew = clockSkew ?? TimeSpan.FromMinutes(1),

            RequireSignedTokens = _key is not null,
            ValidateIssuerSigningKey = _key is not null,
            IssuerSigningKey = _key,
            ValidAlgorithms = _builder.Algorithm != null ? [_builder.Algorithm] : [],
            NameClaimType = ClaimTypes.Name,
            //    NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
        return tvp;
    }

    /// <summary>
    /// Asynchronously validates the specified JWT using the configured validation parameters.
    /// </summary>
    /// <param name="jwt">The JWT compact string to validate.</param>
    /// <param name="clockSkew">Optional clock skew to allow when validating token lifetime.</param>
    /// <returns>A task that represents the asynchronous operation, containing the token validation result.</returns>
    public async Task<TokenValidationResult> ValidateAsync(string jwt, TimeSpan? clockSkew = null)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        ArgumentNullException.ThrowIfNull(_key);

        // validate the token using the parameters
        var validationParameters = GetValidationParameters(clockSkew);

        var handler = new JsonWebTokenHandler();
        return await handler.ValidateTokenAsync(
            jwt, validationParameters).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously validates the specified JWT using the configured validation parameters.
    /// </summary>
    /// <param name="jwt">The JWT compact string to validate.</param>
    /// <param name="clockSkew">Optional clock skew to allow when validating token lifetime.</param>
    /// <returns>The token validation result.</returns>
    public TokenValidationResult Validate(string jwt, TimeSpan? clockSkew = null) => ValidateAsync(jwt, clockSkew).GetAwaiter().GetResult();
}
