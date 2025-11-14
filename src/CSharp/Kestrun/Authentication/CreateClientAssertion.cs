using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Text.Json;

namespace Kestrun.Authentication;

internal static class ClientAssertionHelper
{
    /*  internal static string BuildPrivateKeyJwt(
          X509Certificate2 certificate,
          string clientId,
          string tokenEndpoint)
      {
          var now = DateTimeOffset.UtcNow;

          var key = new X509SecurityKey(certificate)
          {
              KeyId = certificate.Thumbprint
          };

          var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

          var handler = new JsonWebTokenHandler();

          var descriptor = new SecurityTokenDescriptor
          {
              Issuer = clientId,        // iss = client_id
              Audience = tokenEndpoint,   // aud = token endpoint
              Subject = new ClaimsIdentity(
              [
                  new Claim("sub", clientId),
                  new Claim("jti", Guid.NewGuid().ToString("N"))
              ]),
              NotBefore = now.UtcDateTime,
              IssuedAt = now.UtcDateTime,
              Expires = now.AddMinutes(2).UtcDateTime,
              SigningCredentials = creds
          };

          return handler.CreateToken(descriptor);
      }*/

    /// <summary>
    /// Tries to convert the current authorization request into a PAR-based request.
    /// If PAR is not supported or the request fails, it leaves the context unchanged.
    /// </summary>
    /// <param name="context">Redirect context from OnRedirectToIdentityProvider.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="buildClientAssertion">
    /// Optional factory: given the PAR endpoint (audience), returns a client_assertion JWT
    /// or null if no assertion should be sent.
    /// </param>
    public static async Task TryApplyParAsync(
        RedirectContext context,
        Serilog.ILogger logger,
        Func<string, string?>? buildClientAssertion = null)
    {
        var options = context.Options;
        var config = options.Configuration;

        // 1. Find PAR endpoint
        string? parEndpoint = null;

        // Preferred: from discovery document
        if (config is OpenIdConnectConfiguration oidcConfig &&
            oidcConfig.AdditionalData != null &&
            oidcConfig.AdditionalData.TryGetValue("pushed_authorization_request_endpoint", out var parObj) &&
            parObj is string parFromConfig &&
            !string.IsNullOrWhiteSpace(parFromConfig))
        {
            parEndpoint = parFromConfig;
        }
        else if (!string.IsNullOrEmpty(options.Authority))
        {
            // Conservative fallback (Duende /connect/par)
            parEndpoint = options.Authority.TrimEnd('/') + "/connect/par";
        }

        if (string.IsNullOrWhiteSpace(parEndpoint))
        {
            logger.Debug("PAR not applied: no PAR endpoint discovered.");
            return;
        }

        // 2. Build PAR parameters from the current ProtocolMessage
        var msg = context.ProtocolMessage;

        // Safely fetch PKCE params from the message parameters
        _ = msg.Parameters.TryGetValue("code_challenge", out var codeChallenge);
        _ = msg.Parameters.TryGetValue("code_challenge_method", out var codeChallengeMethod);
        var parParameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = msg.RedirectUri,
            ["response_type"] = msg.ResponseType,
            ["scope"] = msg.Scope,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = codeChallengeMethod,
            ["state"] = msg.State,
            ["nonce"] = msg.Nonce
        };

        // Remove null/empty values
        parParameters = parParameters
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // 3. Optional: client_assertion (private_key_jwt) for PAR
        if (buildClientAssertion is not null)
        {
            try
            {
                var assertion = buildClientAssertion(parEndpoint);
                if (!string.IsNullOrWhiteSpace(assertion))
                {
                    parParameters["client_assertion_type"] =
                        "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
                    parParameters["client_assertion"] = assertion;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to build client_assertion for PAR.");
                // We can continue without assertion if server allows it
            }
        }

        var http = options.Backchannel ?? new HttpClient();
        logger.Warning("Sending PAR request to {ParEndpoint}", parEndpoint);

        using var content = new FormUrlEncodedContent(parParameters);
        using var response = await http.PostAsync(parEndpoint, content).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.Warning("PAR request failed: {StatusCode}, body: {Body}", response.StatusCode, body);
            return; // fallback to normal authorize request
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("request_uri", out var ruEl))
            {
                logger.Warning("PAR response missing 'request_uri': {Body}", body);
                return;
            }

            var requestUri = ruEl.GetString();
            if (string.IsNullOrWhiteSpace(requestUri))
            {
                logger.Warning("PAR response 'request_uri' is empty: {Body}", body);
                return;
            }

            logger.Warning("PAR succeeded. request_uri={RequestUri}", requestUri);

            // 4. Switch to PAR mode: the authorization request becomes just (client_id, request_uri)
            msg.Parameters.Clear();
            msg.RequestUri = requestUri;
            msg.ClientId = options.ClientId; // ensure client_id is present

            // NOTE: Most servers ignore other params when request_uri is present.
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to parse PAR response: {Body}", body);
        }
    }


}
