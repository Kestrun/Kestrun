using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Kestrun.Authentication;
using Serilog.Events;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Kestrun.OpenApi;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for adding authentication schemes to the Kestrun host.
/// </summary>
public static class KestrunHostAuthnExtensions
{
    #region Basic Authentication
    /// <summary>
    /// Adds Basic Authentication to the Kestrun host.
    /// <para>Use this for simple username/password authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Basic").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configure">Optional configuration for BasicAuthenticationOptions.</param>
    /// <returns>returns the KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
    this KestrunHost host,
    string scheme = AuthenticationDefaults.BasicSchemeName,
    string? displayName = AuthenticationDefaults.BasicDisplayName,
    Action<BasicAuthenticationOptions>? configure = null
    )
    {
        // Build a prototype options instance (single source of truth)
        var prototype = new BasicAuthenticationOptions { Host = host };

        // Let the caller mutate the prototype
        configure?.Invoke(prototype);

        // Configure validators / claims / OpenAPI on the prototype
        ConfigureBasicAuthValidators(host, prototype);
        ConfigureBasicIssueClaims(host, prototype);
        ConfigureOpenApi(host, scheme, prototype);
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, AuthenticationType.Basic, prototype);
        var h = host.AddAuthentication(
           defaultScheme: scheme,
           buildSchemes: ab =>
           {
               _ = ab.AddScheme<BasicAuthenticationOptions, BasicAuthHandler>(
                   authenticationScheme: scheme,
                   displayName: displayName,
                   configureOptions: opts =>
                   {
                       // Copy from the prototype into the runtime instance
                       prototype.ApplyTo(opts);

                       host.Logger.Debug("Configured Basic Authentication using scheme {Scheme}", scheme);
                   });
           }
       );
        //  register the post-configurer **after** the scheme so it can
        //    read BasicAuthenticationOptions for <scheme>
        return h.AddService(services =>
        {
            _ = services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>>(
                sp => new ClaimPolicyPostConfigurer(
                          scheme,
                          sp.GetRequiredService<
                              IOptionsMonitor<BasicAuthenticationOptions>>()));
        });
    }

    /// <summary>
    /// Adds Basic Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Basic").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configure">The BasicAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
        this KestrunHost host,
        string scheme,
        string? displayName,
        BasicAuthenticationOptions configure
        )
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Basic Authentication with scheme: {Scheme}", scheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(configure);
        // Ensure host is set
        if (configure.Host != host)
        {
            configure.Host = host;
        }
        return host.AddBasicAuthentication(
            scheme: scheme,
            displayName: displayName,
            configure: configure.ApplyTo
        );
    }

    /// <summary>
    /// Configures the validators for Basic authentication.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="opts">The options to configure.</param>
    private static void ConfigureBasicAuthValidators(KestrunHost host, BasicAuthenticationOptions opts)
    {
        var settings = opts.ValidateCodeSettings;
        if (string.IsNullOrWhiteSpace(settings.Code))
        {
            return;
        }

        switch (settings.Language)
        {
            case ScriptLanguage.PowerShell:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building PowerShell validator for Basic authentication");
                }

                opts.ValidateCredentialsAsync = BasicAuthHandler.BuildPsValidator(host, settings);
                break;
            case ScriptLanguage.CSharp:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building C# validator for Basic authentication");
                }

                opts.ValidateCredentialsAsync = BasicAuthHandler.BuildCsValidator(host, settings);
                break;
            case ScriptLanguage.VBNet:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building VB.NET validator for Basic authentication");
                }

                opts.ValidateCredentialsAsync = BasicAuthHandler.BuildVBNetValidator(host, settings);
                break;
            default:
                if (opts.Logger.IsEnabled(LogEventLevel.Warning))
                {
                    opts.Logger.Warning("No valid language specified for Basic authentication");
                }
                break;
        }
    }

    /// <summary>
    /// Configures the issue claims for Basic authentication.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="opts">The options to configure.</param>
    /// <exception cref="NotSupportedException">Thrown when the language is not supported.</exception>
    private static void ConfigureBasicIssueClaims(KestrunHost host, BasicAuthenticationOptions opts)
    {
        var settings = opts.IssueClaimsCodeSettings;
        if (string.IsNullOrWhiteSpace(settings.Code))
        {
            return;
        }

        switch (settings.Language)
        {
            case ScriptLanguage.PowerShell:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building PowerShell Issue Claims for API Basic authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildPsIssueClaims(host, settings);
                break;
            case ScriptLanguage.CSharp:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building C# Issue Claims for API Basic authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildCsIssueClaims(host, settings);
                break;
            case ScriptLanguage.VBNet:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building VB.NET Issue Claims for API Basic authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildVBNetIssueClaims(host, settings);
                break;
            default:
                if (opts.Logger.IsEnabled(LogEventLevel.Warning))
                {
                    opts.Logger.Warning("{language} is not supported for API Basic authentication", settings.Language);
                }
                throw new NotSupportedException("Unsupported language");
        }
    }

    #endregion
    #region GitHub OAuth Authentication
    /// <summary>
    /// Adds GitHub OAuth (Authorization Code) authentication with optional email enrichment.
    /// Creates three schemes: <paramref name="scheme"/>, <paramref name="scheme"/>.Cookies, <paramref name="scheme"/>.Policy.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">Base scheme name (e.g. "GitHub").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="documentationId">Documentation IDs for the authentication scheme.</param>
    /// <param name="description">A description of the authentication scheme.</param>
    /// <param name="deprecated">If true, marks the authentication scheme as deprecated in OpenAPI documentation.</param>
    /// <param name="clientId">GitHub OAuth App Client ID.</param>
    /// <param name="clientSecret">GitHub OAuth App Client Secret.</param>
    /// <param name="callbackPath">The callback path for OAuth redirection (e.g. "/signin-github").</param>
    /// <returns>The configured KestrunHost.</returns>
    public static KestrunHost AddGitHubOAuthAuthentication(
        this KestrunHost host,
        string scheme,
        string? displayName,
        string[]? documentationId,
        string? description,
        bool deprecated,
        string clientId,
        string clientSecret,
        string callbackPath)
    {
        var opts = ConfigureGitHubOAuth2Options(host, clientId, clientSecret, callbackPath);
        ConfigureGitHubClaimMappings(opts);
        opts.DocumentationId = documentationId ?? [];
        if (!string.IsNullOrWhiteSpace(description))
        {
            opts.Description = description;
        }
        opts.Deprecated = deprecated;
        opts.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                await FetchGitHubUserInfoAsync(context);
                await EnrichGitHubEmailClaimAsync(context, host);
            }
        };
        return host.AddOAuth2Authentication(scheme, displayName, opts);
    }

    /// <summary>
    /// Configures OAuth2Options for GitHub authentication.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="clientId">GitHub OAuth App Client ID.</param>
    /// <param name="clientSecret">GitHub OAuth App Client Secret.</param>
    /// <param name="callbackPath">The callback path for OAuth redirection (e.g. "/signin-github").</param>
    /// <returns>The configured OAuth2Options.</returns>
    private static OAuth2Options ConfigureGitHubOAuth2Options(KestrunHost host, string clientId, string clientSecret, string callbackPath)
    {
        return new OAuth2Options()
        {
            Host = host,
            ClientId = clientId,
            ClientSecret = clientSecret,
            CallbackPath = callbackPath,
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
            TokenEndpoint = "https://github.com/login/oauth/access_token",
            UserInformationEndpoint = "https://api.github.com/user",
            SaveTokens = true,
            SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme,
            Scope = { "read:user", "user:email" }
        };
    }

    /// <summary>
    /// Configures claim mappings for GitHub OAuth2Options.
    /// </summary>
    /// <param name="opts">The OAuth2Options to configure.</param>
    private static void ConfigureGitHubClaimMappings(OAuth2Options opts)
    {
        opts.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        opts.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        opts.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        opts.ClaimActions.MapJsonKey("name", "name");
        opts.ClaimActions.MapJsonKey("urn:github:login", "login");
        opts.ClaimActions.MapJsonKey("urn:github:avatar_url", "avatar_url");
        opts.ClaimActions.MapJsonKey("urn:github:html_url", "html_url");
    }

    /// <summary>
    /// Fetches GitHub user information and adds claims to the identity.
    /// </summary>
    /// <param name="context">The OAuthCreatingTicketContext.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task FetchGitHubUserInfoAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Add("User-Agent", "KestrunOAuth/1.0");
        request.Headers.Authorization = new("Bearer", context.AccessToken);

        using var response = await context.Backchannel.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead,
            context.HttpContext.RequestAborted);

        _ = response.EnsureSuccessStatusCode();

        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        context.RunClaimActions(user.RootElement);
    }

    /// <summary>
    /// Fetches GitHub user emails and enriches the identity with the primary verified email claim.
    /// </summary>
    /// <param name="context">The OAuthCreatingTicketContext.</param>
    /// <param name="host">The KestrunHost instance for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task EnrichGitHubEmailClaimAsync(OAuthCreatingTicketContext context, KestrunHost host)
    {
        if (context.Identity is null || context.Identity.HasClaim(c => c.Type == ClaimTypes.Email))
        {
            return;
        }

        try
        {
            using var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
            emailRequest.Headers.Accept.Add(new("application/json"));
            emailRequest.Headers.Add("User-Agent", "KestrunOAuth/1.0");
            emailRequest.Headers.Authorization = new("Bearer", context.AccessToken);

            using var emailResponse = await context.Backchannel.SendAsync(emailRequest,
                HttpCompletionOption.ResponseHeadersRead,
                context.HttpContext.RequestAborted);

            if (!emailResponse.IsSuccessStatusCode)
            {
                return;
            }

            using var emails = JsonDocument.Parse(await emailResponse.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            var primaryEmail = FindPrimaryVerifiedEmail(emails) ?? FindFirstVerifiedEmail(emails);

            if (!string.IsNullOrWhiteSpace(primaryEmail))
            {
                context.Identity.AddClaim(new Claim(
                    ClaimTypes.Email,
                    primaryEmail,
                    ClaimValueTypes.String,
                    context.Options.ClaimsIssuer));
            }
        }
        catch (Exception ex)
        {
            host.Logger.Verbose(exception: ex, messageTemplate: "Failed to enrich GitHub email claim.");
        }
    }

    /// <summary>
    /// Finds the primary verified email from the GitHub emails JSON document.
    /// </summary>
    /// <param name="emails">The JSON document containing GitHub emails.</param>
    /// <returns>The primary verified email if found; otherwise, null.</returns>
    private static string? FindPrimaryVerifiedEmail(JsonDocument emails)
    {
        foreach (var emailObj in emails.RootElement.EnumerateArray())
        {
            var isPrimary = emailObj.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean();
            var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();

            if (isPrimary && isVerified && emailObj.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the primary verified email from the GitHub emails JSON document.
    /// </summary>
    /// <param name="emails">The JSON document containing GitHub emails.</param>
    /// <returns>The primary verified email if found; otherwise, null.</returns>
    private static string? FindFirstVerifiedEmail(JsonDocument emails)
    {
        foreach (var emailObj in emails.RootElement.EnumerateArray())
        {
            var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();
            if (isVerified && emailObj.TryGetProperty("email", out var emailProp))
            {
                return emailProp.GetString();
            }
        }
        return null;
    }

    #endregion
    #region JWT Bearer Authentication
    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host.
    /// <para>Use this for APIs that require token-based authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (e.g. "Bearer").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">Optional configuration for JwtAuthOptions.</param>
    /// <example>
    /// HS512 (HMAC-SHA-512, symmetric)
    /// </example>
    /// <code>
    ///     var hmacKey = new SymmetricSecurityKey(
    ///         Encoding.UTF8.GetBytes("32-bytes-or-more-secret……"));
    ///     host.AddJwtBearerAuthentication(
    ///         scheme:          "Bearer",
    ///         issuer:          "KestrunApi",
    ///         audience:        "KestrunClients",
    ///         validationKey:   hmacKey,
    ///         validAlgorithms: new[] { SecurityAlgorithms.HmacSha512 });
    /// </code>
    /// <example>
    /// RS256 (RSA-SHA-256, asymmetric)
    /// <para>Requires a PEM-encoded private key file.</para>
    /// <code>
    ///    using var rsa = RSA.Create();
    ///     rsa.ImportFromPem(File.ReadAllText("private-key.pem"));
    ///     var rsaKey = new RsaSecurityKey(rsa);
    ///
    ///     host.AddJwtBearerAuthentication(
    ///         scheme:          "Rs256",
    ///         issuer:          "KestrunApi",
    ///         audience:        "KestrunClients",
    ///         validationKey:   rsaKey,
    ///         validAlgorithms: new[] { SecurityAlgorithms.RsaSha256 });
    /// </code>
    /// </example>
    /// <example>
    /// ES256 (ECDSA-SHA-256, asymmetric)
    /// <para>Requires a PEM-encoded private key file.</para>
    /// <code>
    ///     using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    ///     var esKey = new ECDsaSecurityKey(ecdsa);
    ///     host.AddJwtBearerAuthentication(
    ///         "Es256", "KestrunApi", "KestrunClients",
    ///         esKey, new[] { SecurityAlgorithms.EcdsaSha256 });
    /// </code>
    /// </example>
    /// <returns></returns>
    public static KestrunHost AddJwtBearerAuthentication(
      this KestrunHost host,
      string authenticationScheme = AuthenticationDefaults.JwtBearerSchemeName,
      string? displayName = AuthenticationDefaults.JwtBearerDisplayName,
      Action<JwtAuthOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        // Build a prototype options instance (single source of truth)
        var prototype = new JwtAuthOptions { Host = host };
        configureOptions?.Invoke(prototype);
        ConfigureOpenApi(host, authenticationScheme, prototype);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.Bearer, prototype);

        return host.AddAuthentication(
            defaultScheme: authenticationScheme,
            buildSchemes: ab =>
            {
                _ = ab.AddJwtBearer(
                    authenticationScheme: authenticationScheme,
                    displayName: displayName,
                    configureOptions: opts =>
                {
                    prototype.ApplyTo(opts);
                });
            },
            configureAuthz: prototype.ClaimPolicy?.ToAuthzDelegate()
            );
    }

    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">Optional configuration for JwtAuthOptions.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddJwtBearerAuthentication(
        this KestrunHost host,
        string authenticationScheme = AuthenticationDefaults.JwtBearerSchemeName,
        string? displayName = AuthenticationDefaults.JwtBearerDisplayName,
        JwtAuthOptions? configureOptions = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Jwt Bearer Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }

        return host.AddJwtBearerAuthentication(
            authenticationScheme: authenticationScheme,
              displayName: displayName,
              configureOptions: opts =>
              {
                  // Copy relevant properties from provided options instance to the framework-created one
                  configureOptions.ApplyTo(opts);
                  host.Logger.Debug(
                           "Configured JWT Authentication using scheme {Scheme}.",
                           authenticationScheme);
              }
            );
    }
    #endregion
    #region Cookie Authentication
    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host.
    /// <para>Use this for browser-based authentication using cookies.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">Optional configuration for CookieAuthenticationOptions.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddCookieAuthentication(
        this KestrunHost host,
        string authenticationScheme = AuthenticationDefaults.CookiesSchemeName,
        string? displayName = AuthenticationDefaults.CookiesDisplayName,
        Action<CookieAuthOptions>? configureOptions = null,
     ClaimPolicyConfig? claimPolicy = null)
    {
        // Build a prototype options instance (single source of truth)
        var prototype = new CookieAuthOptions { Host = host };
        configureOptions?.Invoke(prototype);
        ConfigureOpenApi(host, authenticationScheme, prototype);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.Cookie, prototype);

        // Add authentication
        return host.AddAuthentication(
            defaultScheme: authenticationScheme,
            buildSchemes: ab =>
            {
                _ = ab.AddCookie(
                    authenticationScheme: authenticationScheme,
                    displayName: displayName,
                    configureOptions: opts =>
                    {
                        // Copy everything from the prototype into the real options instance
                        prototype.ApplyTo(opts);
                        // let caller mutate everything first
                        //configure?.Invoke(opts);
                    });
            },
            configureAuthz: claimPolicy?.ToAuthzDelegate()
        );
    }

    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The CookieAuthenticationOptions object to configure the authentication.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddCookieAuthentication(
          this KestrunHost host,
          string authenticationScheme = AuthenticationDefaults.CookiesSchemeName,
          string? displayName = AuthenticationDefaults.CookiesDisplayName,
          CookieAuthOptions? configureOptions = null,
       ClaimPolicyConfig? claimPolicy = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Cookie Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configureOptions);
        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }
        // Copy relevant properties from provided options instance to the framework-created one
        return host.AddCookieAuthentication(
            authenticationScheme: authenticationScheme,
            displayName: displayName,
            configureOptions: configureOptions.ApplyTo,
            claimPolicy: claimPolicy
        );
    }
    #endregion

    /*
        public static KestrunHost AddClientCertificateAuthentication(
            this KestrunHost host,
            string scheme = CertificateAuthenticationDefaults.AuthenticationScheme,
            Action<CertificateAuthenticationOptions>? configure = null,
            Action<AuthorizationOptions>? configureAuthz = null)
        {
            return host.AddAuthentication(
                defaultScheme: scheme,
                buildSchemes: ab =>
                {
                    ab.AddCertificate(
                        authenticationScheme: scheme,
                        configureOptions: configure ?? (opts => { }));
                },
                configureAuthz: configureAuthz
            );
        }
    */

    #region Windows Authentication

    /// <summary>
    /// Adds Windows Authentication to the Kestrun host.
    /// <para>The authentication scheme name is <see cref="NegotiateDefaults.AuthenticationScheme"/>.
    /// This enables Kerberos and NTLM authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (default is NegotiateDefaults.AuthenticationScheme).</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The WindowsAuthOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddWindowsAuthentication(
       this KestrunHost host,
       string authenticationScheme = AuthenticationDefaults.WindowsSchemeName,
       string? displayName = AuthenticationDefaults.WindowsDisplayName,
       Action<WindowsAuthOptions>? configureOptions = null)
    {
        // Build a prototype options instance (single source of truth)
        var prototype = new WindowsAuthOptions { Host = host };
        configureOptions?.Invoke(prototype);
        ConfigureOpenApi(host, authenticationScheme, prototype);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.Cookie, prototype);

        // Add authentication
        return host.AddAuthentication(
            defaultScheme: authenticationScheme,
            buildSchemes: ab =>
            {
                _ = ab.AddNegotiate(
                    authenticationScheme: authenticationScheme,
                    displayName: displayName,
                    configureOptions: opts =>
                    {
                        // Copy everything from the prototype into the real options instance
                        prototype.ApplyTo(opts);

                        host.Logger.Debug("Configured Windows Authentication using scheme {Scheme}", authenticationScheme);
                    }
                );
            }
        );
    }
    /// <summary>
    /// Adds Windows Authentication to the Kestrun host.
    /// <para>
    /// The authentication scheme name is <see cref="NegotiateDefaults.AuthenticationScheme"/>.
    /// This enables Kerberos and NTLM authentication.
    /// </para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (default is NegotiateDefaults.AuthenticationScheme).</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The WindowsAuthOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddWindowsAuthentication(
        this KestrunHost host,
        string authenticationScheme = AuthenticationDefaults.WindowsSchemeName,
        string? displayName = AuthenticationDefaults.WindowsDisplayName,
        WindowsAuthOptions? configureOptions = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Windows Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(configureOptions);
        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }
        // Copy relevant properties from provided options instance to the framework-created one
        // Add authentication
        return host.AddWindowsAuthentication(
           authenticationScheme: authenticationScheme,
           displayName: displayName,
           configureOptions: configureOptions.ApplyTo
       );
    }

    /// <summary>
    /// Adds Windows Authentication to the Kestrun host.
    /// <para>The authentication scheme name is <see cref="NegotiateDefaults.AuthenticationScheme"/>.
    /// This enables Kerberos and NTLM authentication.</para>
    /// </summary>
    /// <param name="host"> The Kestrun host instance.</param>
    /// <returns> The configured KestrunHost instance.</returns>
    public static KestrunHost AddWindowsAuthentication(this KestrunHost host) =>
        host.AddWindowsAuthentication(
            AuthenticationDefaults.WindowsSchemeName,
            AuthenticationDefaults.WindowsDisplayName,
            (Action<WindowsAuthOptions>?)null);

    #endregion

    #region Client Certificate Authentication

    /// <summary>
    /// Adds Client Certificate Authentication to the Kestrun host.
    /// <para>Use this for authenticating clients using X.509 certificates.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is "Certificate").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configure">Optional configuration for ClientCertificateAuthenticationOptions.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddClientCertificateAuthentication(
        this KestrunHost host,
        string scheme = AuthenticationDefaults.CertificateSchemeName,
        string? displayName = AuthenticationDefaults.CertificateDisplayName,
        Action<ClientCertificateAuthenticationOptions>? configure = null)
    {
        // Build a prototype options instance (single source of truth)
        var prototype = new ClientCertificateAuthenticationOptions { Host = host };

        // Let the caller mutate the prototype
        configure?.Invoke(prototype);

        ConfigureOpenApi(host, scheme, prototype);

        // Register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, AuthenticationType.Certificate, prototype);

        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddScheme<ClientCertificateAuthenticationOptions, ClientCertificateAuthHandler>(
                    authenticationScheme: scheme,
                    displayName: displayName,
                    configureOptions: opts =>
                    {
                        // Copy from the prototype into the runtime instance
                        prototype.ApplyTo(opts);

                        host.Logger.Debug("Configured Client Certificate Authentication using scheme {Scheme}", scheme);
                    });
            }
        );
    }

    /// <summary>
    /// Adds Client Certificate Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is "Certificate").</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configure">The ClientCertificateAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddClientCertificateAuthentication(
        this KestrunHost host,
        string scheme,
        string? displayName,
        ClientCertificateAuthenticationOptions configure)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Client Certificate Authentication with scheme: {Scheme}", scheme);
        }

        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(configure);

        // Ensure host is set
        if (configure.Host != host)
        {
            configure.Host = host;
        }

        return host.AddClientCertificateAuthentication(
            scheme: scheme,
            displayName: displayName,
            configure: configure.ApplyTo
        );
    }

    /// <summary>
    /// Adds Client Certificate Authentication to the Kestrun host with default settings.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddClientCertificateAuthentication(this KestrunHost host) =>
        host.AddClientCertificateAuthentication(
            AuthenticationDefaults.CertificateSchemeName,
            AuthenticationDefaults.CertificateDisplayName,
            (Action<ClientCertificateAuthenticationOptions>?)null);

    #endregion
    #region API Key Authentication
    /// <summary>
    /// Adds API Key Authentication to the Kestrun host.
    /// <para>Use this for endpoints that require an API key for access.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name (default is "ApiKey").</param>
    /// <param name="displayName">The display name for the authentication scheme (default is "API Key").</param>
    /// <param name="configureOptions">Optional configuration for ApiKeyAuthenticationOptions.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string authenticationScheme = AuthenticationDefaults.ApiKeySchemeName,
    string? displayName = AuthenticationDefaults.ApiKeyDisplayName,
    Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        // Build a prototype options instance (single source of truth)
        var prototype = new ApiKeyAuthenticationOptions { Host = host };

        // Let the caller mutate the prototype
        configureOptions?.Invoke(prototype);

        // Configure validators / claims / OpenAPI on the prototype
        ConfigureApiKeyValidators(host, prototype);
        ConfigureApiKeyIssueClaims(host, prototype);
        ConfigureOpenApi(host, authenticationScheme, prototype);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.ApiKey, prototype);
        // Add authentication
        return host.AddAuthentication(
             defaultScheme: authenticationScheme,
             buildSchemes: ab =>
             {
                 // ← TOptions == ApiKeyAuthenticationOptions
                 //    THandler == ApiKeyAuthHandler
                 _ = ab.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthHandler>(
                     authenticationScheme: authenticationScheme,
                     displayName: displayName,
                     configureOptions: opts =>
                     {
                         // Copy from the prototype into the runtime instance
                         prototype.ApplyTo(opts);

                         host.Logger.Debug(
                             "Configured API Key Authentication using scheme {Scheme} with header {Header} (In={In})",
                             authenticationScheme, prototype.ApiKeyName, prototype.In);
                     });
             }
         )
        //  register the post-configurer **after** the scheme so it can
        //    read BasicAuthenticationOptions for <scheme>
        .AddService(services =>
          {
              _ = services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>>(
                  sp => new ClaimPolicyPostConfigurer(
                            authenticationScheme,
                            sp.GetRequiredService<
                                IOptionsMonitor<ApiKeyAuthenticationOptions>>()));
          });
    }

    /// <summary>
    /// Adds API Key Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The ApiKeyAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string authenticationScheme = AuthenticationDefaults.ApiKeySchemeName,
    string? displayName = AuthenticationDefaults.ApiKeyDisplayName,
    ApiKeyAuthenticationOptions? configureOptions = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding API Key Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configureOptions);
        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }
        // Copy properties from the provided configure object
        return host.AddApiKeyAuthentication(
            authenticationScheme: authenticationScheme,
            displayName: displayName,
            configureOptions: configureOptions.ApplyTo
        );
    }

    /// <summary>
    /// Configures the API Key validators.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="opts">The options to configure.</param>
    /// <exception cref="NotSupportedException">Thrown when the language is not supported.</exception>
    private static void ConfigureApiKeyValidators(KestrunHost host, ApiKeyAuthenticationOptions opts)
    {
        var settings = opts.ValidateCodeSettings;
        if (string.IsNullOrWhiteSpace(settings.Code))
        {
            return;
        }

        switch (settings.Language)
        {
            case ScriptLanguage.PowerShell:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building PowerShell validator for API Key authentication");
                }

                opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildPsValidator(host, settings);
                break;
            case ScriptLanguage.CSharp:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building C# validator for API Key authentication");
                }

                opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildCsValidator(host, settings);
                break;
            case ScriptLanguage.VBNet:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building VB.NET validator for API Key authentication");
                }

                opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildVBNetValidator(host, settings);
                break;
            default:
                if (opts.Logger.IsEnabled(LogEventLevel.Warning))
                {
                    opts.Logger.Warning("{language} is not supported for API Basic authentication", settings.Language);
                }
                throw new NotSupportedException("Unsupported language");
        }
    }

    /// <summary>
    /// Configures the API Key issue claims.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="opts">The options to configure.</param>
    /// <exception cref="NotSupportedException">Thrown when the language is not supported.</exception>
    private static void ConfigureApiKeyIssueClaims(KestrunHost host, ApiKeyAuthenticationOptions opts)
    {
        var settings = opts.IssueClaimsCodeSettings;
        if (string.IsNullOrWhiteSpace(settings.Code))
        {
            return;
        }

        switch (settings.Language)
        {
            case ScriptLanguage.PowerShell:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building PowerShell Issue Claims for API Key authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildPsIssueClaims(host, settings);
                break;
            case ScriptLanguage.CSharp:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building C# Issue Claims for API Key authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildCsIssueClaims(host, settings);
                break;
            case ScriptLanguage.VBNet:
                if (opts.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    opts.Logger.Debug("Building VB.NET Issue Claims for API Key authentication");
                }

                opts.IssueClaims = IAuthHandler.BuildVBNetIssueClaims(host, settings);
                break;
            default:
                if (opts.Logger.IsEnabled(LogEventLevel.Warning))
                {
                    opts.Logger.Warning("{language} is not supported for API Basic authentication", settings.Language);
                }
                throw new NotSupportedException("Unsupported language");
        }
    }

    #endregion

    #region OAuth2 Authentication

    /// <summary>
    /// Adds OAuth2 authentication to the Kestrun host.
    /// <para>Use this for applications that require OAuth2 authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The OAuth2Options to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOAuth2Authentication(
        this KestrunHost host,
        string authenticationScheme = AuthenticationDefaults.OAuth2SchemeName,
        string? displayName = AuthenticationDefaults.OAuth2DisplayName,
        OAuth2Options? configureOptions = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding OAuth2 Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Required for OAuth2
        if (string.IsNullOrWhiteSpace(configureOptions.ClientId))
        {
            throw new ArgumentException("ClientId must be provided in OAuth2Options", nameof(configureOptions));
        }

        if (string.IsNullOrWhiteSpace(configureOptions.AuthorizationEndpoint))
        {
            throw new ArgumentException("AuthorizationEndpoint must be provided in OAuth2Options", nameof(configureOptions));
        }

        if (string.IsNullOrWhiteSpace(configureOptions.TokenEndpoint))
        {
            throw new ArgumentException("TokenEndpoint must be provided in OAuth2Options", nameof(configureOptions));
        }

        // Default CallbackPath if not set: /signin-{scheme}
        if (string.IsNullOrWhiteSpace(configureOptions.CallbackPath))
        {
            configureOptions.CallbackPath = $"/signin-{authenticationScheme.ToLowerInvariant()}";
        }
        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }
        // Ensure scheme is set
        if (authenticationScheme != configureOptions.AuthenticationScheme)
        {
            configureOptions.AuthenticationScheme = authenticationScheme;
        }
        // Configure scopes and claim policies
        ConfigureScopes(configureOptions, host.Logger);
        // Configure OpenAPI
        ConfigureOpenApi(host, authenticationScheme, configureOptions);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.OAuth2, configureOptions);

        // Add authentication
        return host.AddAuthentication(
            defaultScheme: configureOptions.CookieScheme,
            defaultChallengeScheme: authenticationScheme,
            buildSchemes: ab =>
            {
                // Add cookie scheme for sign-in
                _ = ab.AddCookie(configureOptions.CookieScheme, cookieOpts =>
               {
                   configureOptions.CookieOptions.ApplyTo(cookieOpts);
               });
                // Add OAuth2 scheme
                _ = ab.AddOAuth(
                    authenticationScheme: authenticationScheme,
                    displayName: displayName ?? OAuthDefaults.DisplayName,
                    configureOptions: oauthOpts =>
                {
                    configureOptions.ApplyTo(oauthOpts);
                    if (host.Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        host.Logger.Debug("Configured OpenID Connect with ClientId: {ClientId}, Scopes: {Scopes}",
                          oauthOpts.ClientId, string.Join(", ", oauthOpts.Scope));
                    }
                });
            },
              configureAuthz: configureOptions.ClaimPolicy?.ToAuthzDelegate()
        );
    }

    /// <summary>
    /// Configures OAuth2 scopes and claim policies.
    /// </summary>
    /// <param name="configureOptions">The OAuth2 options to configure.</param>
    /// <param name="logger">The logger for debug output.</param>
    private static void ConfigureScopes(IOAuthCommonOptions configureOptions, Serilog.ILogger logger)
    {
        if (configureOptions.Scope is null)
        {
            return;
        }

        if (configureOptions.Scope.Count == 0)
        {
            BackfillScopesFromClaimPolicy(configureOptions, logger);
            return;
        }

        LogConfiguredScopes(configureOptions.Scope, logger);

        if (configureOptions.ClaimPolicy is null)
        {
            configureOptions.ClaimPolicy = BuildClaimPolicyFromScopes(configureOptions.Scope, logger);
            return;
        }

        AddMissingScopesToClaimPolicy(configureOptions.Scope, configureOptions.ClaimPolicy, logger);
    }

    private static ClaimPolicyConfig BuildClaimPolicyFromScopes(ICollection<string> scopes, Serilog.ILogger logger)
    {
        var claimPolicyBuilder = new ClaimPolicyBuilder();
        foreach (var scope in scopes)
        {
            LogScopeAdded(logger, scope);
            _ = claimPolicyBuilder.AddPolicy(policyName: scope, claimType: "scope", description: string.Empty, allowedValues: scope);
        }

        return claimPolicyBuilder.Build();
    }

    private static void AddMissingScopesToClaimPolicy(ICollection<string> scopes, ClaimPolicyConfig claimPolicy, Serilog.ILogger logger)
    {
        var missingScopes = scopes
            .Where(s => !claimPolicy.Policies.ContainsKey(s))
            .ToList();

        if (missingScopes.Count == 0)
        {
            return;
        }

        LogMissingScopes(missingScopes, logger);

        var claimPolicyBuilder = new ClaimPolicyBuilder();
        foreach (var scope in missingScopes)
        {
            _ = claimPolicyBuilder.AddPolicy(policyName: scope, claimType: "scope", description: string.Empty, allowedValues: scope);
            LogScopeAddedToClaimPolicy(logger, scope);
        }

        claimPolicy.AddPolicies(claimPolicyBuilder.Policies);
    }

    private static void BackfillScopesFromClaimPolicy(IOAuthCommonOptions configureOptions, Serilog.ILogger logger)
    {
        if (configureOptions.ClaimPolicy is null)
        {
            return;
        }

        foreach (var policy in configureOptions.ClaimPolicy.PolicyNames)
        {
            LogClaimPolicyConfigured(logger, policy);
            configureOptions.Scope?.Add(policy);
        }
    }

    private static void LogScopeAdded(Serilog.ILogger logger, string scope)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("OAuth2 scope added: {Scope}", scope);
        }
    }

    private static void LogScopeAddedToClaimPolicy(Serilog.ILogger logger, string scope)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("OAuth2 scope added to claim policy: {Scope}", scope);
        }
    }

    private static void LogMissingScopes(IEnumerable<string> missingScopes, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("Adding missing OAuth2 scopes to claim policy: {Scopes}", string.Join(", ", missingScopes));
        }
    }

    private static void LogConfiguredScopes(IEnumerable<string> scopes, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("OAuth2 scopes configured: {Scopes}", string.Join(", ", scopes));
        }
    }

    private static void LogClaimPolicyConfigured(Serilog.ILogger logger, string policy)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("OAuth2 claim policy configured: {Policy}", policy);
        }
    }

    #endregion
    #region OpenID Connect Authentication

    /// <summary>
    /// Adds OpenID Connect authentication to the Kestrun host with private key JWT client assertion.
    /// <para>Use this for applications that require OpenID Connect authentication with client credentials using JWT assertion.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="authenticationScheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configureOptions">The OpenIdConnectOptions to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOpenIdConnectAuthentication(
           this KestrunHost host,
           string authenticationScheme = AuthenticationDefaults.OidcSchemeName,
           string? displayName = AuthenticationDefaults.OidcDisplayName,
           OidcOptions? configureOptions = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding OpenID Connect Authentication with scheme: {Scheme}", authenticationScheme);
        }
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(authenticationScheme);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Ensure ClientId is set
        if (string.IsNullOrWhiteSpace(configureOptions.ClientId))
        {
            throw new ArgumentException("ClientId must be provided in OpenIdConnectOptions", nameof(configureOptions));
        }
        // Ensure host is set
        if (configureOptions.Host != host)
        {
            configureOptions.Host = host;
        }
        // Ensure scheme is set
        if (authenticationScheme != configureOptions.AuthenticationScheme)
        {
            configureOptions.AuthenticationScheme = authenticationScheme;
        }
        // Retrieve supported scopes from the OIDC provider
        if (!string.IsNullOrWhiteSpace(configureOptions.Authority))
        {
            configureOptions.ClaimPolicy = GetSupportedScopes(configureOptions.Authority, host.Logger);
            if (host.Logger.IsEnabled(LogEventLevel.Debug))
            {
                host.Logger.Debug("OIDC supported scopes: {Scopes}", string.Join(", ", configureOptions.ClaimPolicy?.Policies.Keys ?? Enumerable.Empty<string>()));
            }
        }
        // Configure scopes and claim policies
        ConfigureScopes(configureOptions, host.Logger);
        // Configure OpenAPI
        ConfigureOpenApi(host, authenticationScheme, configureOptions);

        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(authenticationScheme, AuthenticationType.Oidc, configureOptions);

        // CRITICAL: Register OidcEvents and AssertionService in DI before configuring authentication
        // This is required because EventsType expects these to be available in the service provider
        return host.AddService(services =>
         {
             // Register AssertionService as a singleton with factory to pass clientId and jwkJson
             // Only register if JwkJson is provided (for private_key_jwt authentication)
             if (!string.IsNullOrWhiteSpace(configureOptions.JwkJson))
             {
                 services.TryAddSingleton(sp => new AssertionService(configureOptions.ClientId, configureOptions.JwkJson));
                 // Register OidcEvents as scoped (per-request)
                 services.TryAddScoped<OidcEvents>();
             }
         }).AddAuthentication(
              defaultScheme: configureOptions.CookieScheme,
              defaultChallengeScheme: authenticationScheme,
              buildSchemes: ab =>
              {
                  // Add cookie scheme for sign-in
                  _ = ab.AddCookie(configureOptions.CookieScheme, cookieOpts =>
                 {
                     // Copy cookie configuration from options.CookieOptions
                     configureOptions.CookieOptions.ApplyTo(cookieOpts);
                 });
                  // Add OpenID Connect scheme
                  _ = ab.AddOpenIdConnect(
                    authenticationScheme: authenticationScheme,
                    displayName: displayName ?? OpenIdConnectDefaults.DisplayName,
                    configureOptions: oidcOpts =>
                 {
                     // Copy all properties from the provided options to the framework's options
                     configureOptions.ApplyTo(oidcOpts);

                     // Inject private key JWT at code → token step (only if JwkJson is provided)
                     // This will be resolved from DI at runtime
                     if (!string.IsNullOrWhiteSpace(configureOptions.JwkJson))
                     {
                         oidcOpts.EventsType = typeof(OidcEvents);
                     }
                     if (host.Logger.IsEnabled(LogEventLevel.Debug))
                     {
                         host.Logger.Debug("Configured OpenID Connect with Authority: {Authority}, ClientId: {ClientId}, Scopes: {Scopes}",
                             oidcOpts.Authority, oidcOpts.ClientId, string.Join(", ", oidcOpts.Scope));
                     }
                 });
              },
              configureAuthz: configureOptions.ClaimPolicy?.ToAuthzDelegate()
            );
    }

    /// <summary>
    /// Retrieves the supported scopes from the OpenID Connect provider's metadata.
    /// </summary>
    /// <param name="authority">The authority URL of the OpenID Connect provider.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <returns>A ClaimPolicyConfig containing the supported scopes, or null if retrieval fails.</returns>
    private static ClaimPolicyConfig? GetSupportedScopes(string authority, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("Retrieving OpenID Connect configuration from authority: {Authority}", authority);
        }
        var claimPolicy = new ClaimPolicyBuilder();
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new ArgumentException("Authority must be provided to retrieve OpenID Connect scopes.", nameof(authority));
        }

        var metadataAddress = authority.TrimEnd('/') + "/.well-known/openid-configuration";

        var documentRetriever = new HttpDocumentRetriever
        {
            RequireHttps = metadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        };

        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever);

        try
        {
            var cfg = configManager.GetConfigurationAsync(CancellationToken.None)
                                   .GetAwaiter()
                                   .GetResult();
            // First try the strongly-typed property
            var scopes = cfg.ScopesSupported;

            // If it's null or empty, fall back to raw JSON
            if (scopes == null || scopes.Count == 0)
            {
                var json = documentRetriever.GetDocumentAsync(metadataAddress, CancellationToken.None)
                                            .GetAwaiter()
                                            .GetResult();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("scopes_supported", out var scopesElement) &&
                    scopesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var scope in scopesElement.EnumerateArray().Select(item => item.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        if (scope != null)
                        {
                            _ = claimPolicy.AddPolicy(policyName: scope, claimType: "scope", description: string.Empty, allowedValues: scope);
                        }
                    }
                }
            }
            else
            {
                // Normal path: configuration object had scopes
                foreach (var scope in scopes)
                {
                    _ = claimPolicy.AddPolicy(policyName: scope, claimType: "scope", description: string.Empty, allowedValues: scope);
                }
            }
            return claimPolicy.Build();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to retrieve OpenID Connect configuration from {MetadataAddress}", metadataAddress);
            return null;
        }
    }

    #endregion
    #region Helper Methods
    /// <summary>
    /// Configures OpenAPI security schemes for the given authentication options.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="opts">The OpenAPI authentication options.</param>
    private static void ConfigureOpenApi(KestrunHost host, string scheme, IOpenApiAuthenticationOptions opts)
    {
        // Apply to specified documentation IDs or all if none specified
        if (opts.DocumentationId == null || opts.DocumentationId.Length == 0)
        {
            opts.DocumentationId = OpenApiDocDescriptor.DefaultDocumentationIds;
        }

        foreach (var docDescriptor in opts.DocumentationId
            .Select(host.GetOrCreateOpenApiDocument)
            .Where(docDescriptor => docDescriptor != null))
        {
            docDescriptor.ApplySecurityScheme(scheme, opts);
        }
    }

    #endregion

    /// <summary>
    /// Adds authentication and authorization middleware to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="buildSchemes">A delegate to configure authentication schemes.</param>
    /// <param name="defaultScheme">The default authentication scheme.</param>
    /// <param name="configureAuthz">Optional authorization policy configuration.</param>
    /// <param name="defaultChallengeScheme">The default challenge scheme .</param>
    /// <returns>The configured KestrunHost instance.</returns>
    internal static KestrunHost AddAuthentication(
    this KestrunHost host,
    string defaultScheme,
    Action<AuthenticationBuilder>? buildSchemes = null,    // e.g., ab => ab.AddCookie().AddOpenIdConnect("oidc", ...)
    Action<AuthorizationOptions>? configureAuthz = null,
    string? defaultChallengeScheme = null)
    {
        ArgumentNullException.ThrowIfNull(buildSchemes);
        if (string.IsNullOrWhiteSpace(defaultScheme))
        {
            throw new ArgumentException("Default scheme is required.", nameof(defaultScheme));
        }

        _ = host.AddService(services =>
        {
            // CRITICAL: Check if authentication services are already registered
            // If they are, we only need to add new schemes, not reconfigure defaults
            var authDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuthenticationService));

            AuthenticationBuilder authBuilder;
            if (authDescriptor != null)
            {
                // Authentication already registered - only add new schemes without changing defaults
                host.Logger.Debug("Authentication services already registered - adding schemes only (default={DefaultScheme})", defaultScheme);
                authBuilder = new AuthenticationBuilder(services);
            }
            else
            {
                // First time registration - configure defaults
                host.Logger.Debug(
                    "Registering authentication services with defaults (default={DefaultScheme}, challenge={ChallengeScheme})",
                    defaultScheme,
                    defaultChallengeScheme ?? defaultScheme);
                authBuilder = services.AddAuthentication(options =>
                {
                    options.DefaultScheme = defaultScheme;
                    options.DefaultChallengeScheme = defaultChallengeScheme ?? defaultScheme;
                });
            }

            // Let caller add handlers/schemes
            buildSchemes?.Invoke(authBuilder);

            // Ensure Authorization is available (with optional customization)
            // AddAuthorization is idempotent - safe to call multiple times
            _ = configureAuthz is not null ?
                services.AddAuthorization(configureAuthz) :
                services.AddAuthorization();
        });

        // Add middleware once
        return host.Use(app =>
        {
            const string Key = "__kr.authmw";
            if (!app.Properties.ContainsKey(Key))
            {
                _ = app.UseAuthentication();
                _ = app.UseAuthorization();
                app.Properties[Key] = true;
                host.Logger.Information("Kestrun: Authentication & Authorization middleware added.");
            }
        });
    }

    /// <summary>
    /// Checks if the specified authentication scheme is registered in the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="schemeName">The name of the authentication scheme to check.</param>
    /// <returns>True if the scheme is registered; otherwise, false.</returns>
    public static bool HasAuthScheme(this KestrunHost host, string schemeName)
    {
        var schemeProvider = host.App.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = schemeProvider.GetSchemeAsync(schemeName).GetAwaiter().GetResult();
        return scheme != null;
    }

    /// <summary>
    /// Adds authorization services to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="cfg">Optional configuration for authorization options.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddAuthorization(this KestrunHost host, Action<AuthorizationOptions>? cfg = null)
    {
        return host.AddService(services =>
        {
            _ = cfg == null ? services.AddAuthorization() : services.AddAuthorization(cfg);
        });
    }

    /// <summary>
    /// Checks if the specified authorization policy is registered in the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="policyName">The name of the authorization policy to check.</param>
    /// <returns>True if the policy is registered; otherwise, false.</returns>
    public static bool HasAuthPolicy(this KestrunHost host, string policyName)
    {
        var policyProvider = host.App.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = policyProvider.GetPolicyAsync(policyName).GetAwaiter().GetResult();
        return policy != null;
    }

    /// <summary>
    /// HTTP message handler that logs all HTTP requests and responses for debugging.
    /// </summary>
    internal class LoggingHttpMessageHandler(HttpMessageHandler innerHandler, Serilog.ILogger logger) : DelegatingHandler(innerHandler)
    {
        private readonly Serilog.ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // CRITICAL: Static field to store the last token response body so we can manually parse it
        // The framework's OpenIdConnectMessage parser fails to populate AccessToken correctly
        internal static string? LastTokenResponseBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Log request
            _logger.Warning($"HTTP {request.Method} {request.RequestUri}");

            // Check if this is a token endpoint request
            var isTokenEndpoint = request.RequestUri?.PathAndQuery?.Contains("/connect/token") == true ||
                                 request.RequestUri?.PathAndQuery?.Contains("/token") == true;

            if (request.Content != null && !isTokenEndpoint)
            {
                // Read request body without consuming it (only for non-token requests)
                var requestBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                var requestBody = System.Text.Encoding.UTF8.GetString(requestBytes);
                _logger.Warning($"Request Body: {requestBody}");

                // Recreate the content so it can be read again
                request.Content = new ByteArrayContent(requestBytes);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            }
            else if (request.Content != null && isTokenEndpoint)
            {
                _logger.Warning("Token endpoint request - skipping body logging to preserve stream");
            }

            // Send request
            var response = await base.SendAsync(request, cancellationToken);

            // Log response
            _logger.Warning($"HTTP Response: {(int)response.StatusCode} {response.StatusCode}");

            // CRITICAL: For token endpoint responses, capture the body for manual parsing
            // but then recreate the stream so the framework can also read it
            if (response.Content != null && isTokenEndpoint)
            {
                // Read the response body
                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var responseBody = System.Text.Encoding.UTF8.GetString(responseBytes);

                // Store it in static field for later manual parsing
                LastTokenResponseBody = responseBody;
                _logger.Warning($"Captured token response body ({responseBytes.Length} bytes) for manual parsing");

                // Recreate the content stream with ALL original headers preserved
                var originalHeaders = response.Content.Headers.ToList();
                var newContent = new ByteArrayContent(responseBytes);

                foreach (var header in originalHeaders)
                {
                    _ = newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content = newContent;
                _logger.Warning("Recreated token response stream for framework parsing");
            }
            else if (response.Content != null && !isTokenEndpoint)
            {
                // Save original headers
                var originalHeaders = response.Content.Headers;

                // Read response body and preserve it for the handler
                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var responseBody = System.Text.Encoding.UTF8.GetString(responseBytes);
                _logger.Warning($"Response Body: {responseBody}");

                // Recreate the content so it can be read again by the OIDC handler
                var newContent = new ByteArrayContent(responseBytes);

                // Copy all original headers to the new content
                foreach (var header in originalHeaders)
                {
                    _ = newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                response.Content = newContent;
            }
            else if (response.Content != null && isTokenEndpoint)
            {
                _logger.Warning("Token endpoint response - skipping body logging to let framework parse it");
            }

            return response;
        }
    }
}
