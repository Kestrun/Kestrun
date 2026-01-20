using Microsoft.OpenApi;
using Kestrun.Authentication;

namespace Kestrun.OpenApi;
/// <summary>
/// Methods for applying security schemes to the OpenAPI document.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Applies a security scheme to the OpenAPI document based on the provided authentication options.
    /// </summary>
    /// <param name="scheme">The name of the security scheme.</param>
    /// <param name="options">The authentication options.</param>
    /// <exception cref="NotSupportedException">Thrown when the authentication options type is not supported.</exception>
    public void ApplySecurityScheme(string scheme, IOpenApiAuthenticationOptions options)
    {
        var securityScheme = options switch
        {
            ApiKeyAuthenticationOptions apiKeyOptions => GetSecurityScheme(apiKeyOptions),
            BasicAuthenticationOptions basicOptions => GetSecurityScheme(basicOptions),
            CookieAuthOptions cookieOptions => GetSecurityScheme(cookieOptions),
            JwtAuthOptions jwtOptions => GetSecurityScheme(jwtOptions),
            OAuth2Options oauth2Options => GetSecurityScheme(oauth2Options),
            OidcOptions oidcOptions => GetSecurityScheme(oidcOptions),
            WindowsAuthOptions windowsOptions => GetSecurityScheme(windowsOptions),
            ClientCertificateAuthenticationOptions clientCertificateOptions => GetSecurityScheme(clientCertificateOptions),
            _ => throw new NotSupportedException($"Unsupported authentication options type: {options.GetType().FullName}"),
        };
        AddSecurityComponent(scheme: scheme, globalScheme: options.GlobalScheme, securityScheme: securityScheme);
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for mutual TLS (client certificate) authentication.
    /// </summary>
    /// <param name="options">The client certificate authentication options.</param>
    /// <returns>The OpenAPI security scheme for mutual TLS authentication.</returns>
    private static OpenApiSecurityScheme GetSecurityScheme(ClientCertificateAuthenticationOptions options)
    {
        var tempDisclaimer = "Mutual TLS (client certificate) authentication. Requires mutual TLS (client certificate authentication) at the transport layer." +
             "This is NOT a header-based auth scheme; it is documented here because OpenAPI 3.1 mutualTLS " +
             "is not currently supported by Microsoft.OpenApi.";
        return new OpenApiSecurityScheme
        {
            // Placeholder only — there is no native mutualTLS type in Microsoft.OpenApi today.
            Type = SecuritySchemeType.ApiKey,
            Name = "mTLS",
            In = ParameterLocation.Header,
            Description = string.IsNullOrEmpty(options.Description) ? tempDisclaimer : options.Description + '\n' + tempDisclaimer,

            Deprecated = options.Deprecated,
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                ["x-mtls"] = new JsonNodeExtension(true),
                ["x-transport-auth"] = new JsonNodeExtension("mutualTLS")
            }
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for Windows authentication.
    /// </summary>
    /// <param name="options">The Windows authentication options.</param>
    /// <returns>The OpenAPI security scheme for Windows authentication.</returns>
    private static OpenApiSecurityScheme GetSecurityScheme(WindowsAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = options.Protocol == WindowsAuthProtocol.Ntlm ? "ntlm" : "negotiate",
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for OIDC authentication.
    /// </summary>
    /// <param name="options">The OIDC authentication options.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when neither Authority nor MetadataAddress is set.</exception>
    private static OpenApiSecurityScheme GetSecurityScheme(OidcOptions options)
    {
        // Prefer explicit MetadataAddress if set
        var discoveryUrl = options.MetadataAddress
                           ?? (options.Authority is null
                               ? throw new InvalidOperationException(
                                   "Either Authority or MetadataAddress must be set to build OIDC OpenAPI scheme.")
                               : $"{options.Authority.TrimEnd('/')}/.well-known/openid-configuration");

        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri(discoveryUrl, UriKind.Absolute),
            // Description comes from AuthenticationSchemeOptions base class
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for OAuth2 authentication.
    /// </summary>
    /// <param name="options">The OAuth2 authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(OAuth2Options options)
    {
        // Build OAuth flows
        var flows = new OpenApiOAuthFlows
        {
            // Client Credentials flow
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(options.AuthorizationEndpoint, UriKind.Absolute),
            }
        };
        // Scopes
        if (options.ClaimPolicy is not null && options.ClaimPolicy.Policies is not null && options.ClaimPolicy.Policies.Count > 0)
        {
            var scopes = new Dictionary<string, string>();
            var policies = options.ClaimPolicy.Policies;
            foreach (var item in policies)
            {
                scopes.Add(item.Key, item.Value.Description ?? string.Empty);
            }
            flows.AuthorizationCode.Scopes = scopes;
        }
        // Token endpoint
        if (options.TokenEndpoint is not null)
        {
            flows.AuthorizationCode.TokenUrl = new Uri(options.TokenEndpoint, UriKind.Absolute);
        }

        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = flows,
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }
    /// <summary>
    /// Gets the OpenAPI security scheme for API key authentication.
    /// </summary>
    /// <param name="options">The API key authentication options.</param>
    private static OpenApiSecurityScheme GetSecurityScheme(ApiKeyAuthenticationOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.ApiKey,
            Name = options.ApiKeyName,
            In = options.In,
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for cookie authentication.
    /// </summary>
    /// <param name="options">The cookie authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(CookieAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.ApiKey,
            Name = options.Cookie.Name,
            In = ParameterLocation.Cookie,
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for JWT authentication.
    /// </summary>
    /// <param name="options">The JWT authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(JwtAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    ///  Gets the OpenAPI security scheme for basic authentication.
    /// </summary>
    /// <param name="options">The basic authentication options.</param>
    private static OpenApiSecurityScheme GetSecurityScheme(BasicAuthenticationOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = options.Description,
            Deprecated = options.Deprecated
        };
    }

    /// <summary>
    /// Adds a security component to the OpenAPI document.
    /// </summary>
    /// <param name="scheme">The name of the security component.</param>
    /// <param name="globalScheme">Indicates whether the security scheme should be applied globally.</param>
    /// <param name="securityScheme">The security scheme to add.</param>
    private void AddSecurityComponent(string scheme, bool globalScheme, OpenApiSecurityScheme securityScheme)
    {
        _ = Document.AddComponent(scheme, securityScheme);

        // Reference it by NAME in the requirement (no .Reference in v2)
        var requirement = new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(scheme,Document), new List<string>()
            }
        };
        SecurityRequirement.Add(scheme, requirement);

        // Apply globally if specified
        if (globalScheme)
        {
            // Apply globally
            Document.Security ??= [];
            Document.Security.Add(requirement);
        }
    }
}
