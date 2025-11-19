using System.IdentityModel.Tokens.Jwt;

namespace Kestrun.Jwt;
/// <summary>
/// Provides methods for inspecting and extracting parameters from JWT tokens.
/// </summary>
public static class JwtInspector
{
    /// <summary>
    /// Reads out every header field, standard property, and claim from a compact JWT.
    /// </summary>
    public static JwtParameters ReadAllParameters(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        // parse without validating signature or lifetime
        var jwt = handler.ReadJwtToken(token);

        var result = new JwtParameters
        {
            Issuer = jwt.Issuer,
            Audiences = jwt.Audiences,
            Subject = jwt.Subject,
            NotBefore = jwt.ValidFrom == DateTime.MinValue ? null : jwt.ValidFrom,
            Expires = jwt.ValidTo == DateTime.MinValue ? null : jwt.ValidTo,
            IssuedAt = jwt.Payload.IssuedAt == DateTime.MinValue ? null : jwt.Payload.IssuedAt,
            Algorithm = jwt.SignatureAlgorithm,
            Type = jwt.Header.Typ,
            KeyId = jwt.Header.Kid
        };

        // copy all header entries
        foreach (var kv in jwt.Header)
        {
            result.Header[kv.Key] = kv.Value;
        }

        // copy all payload claims (including custom ones)
        foreach (var claim in jwt.Claims)
        {
            // if a claim type can appear multiple times, you might want to handle lists
            result.Claims[claim.Type] = claim.Value;
        }

        return result;
    }
}
