using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Kestrun.Authentication;
using Serilog.Events;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Kestrun.Claims;

using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <param name="configure">Optional configuration for BasicAuthenticationOptions.</param>
    /// <returns>returns the KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
    this KestrunHost host,
    string scheme = "Basic",
    Action<BasicAuthenticationOptions>? configure = null
    )
    {
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "Basic", configure);
        var h = host.AddAuthentication(
           defaultScheme: scheme,
           buildSchemes: ab =>
           {
               // ← TOptions == BasicAuthenticationOptions
               //    THandler == BasicAuthHandler
               _ = ab.AddScheme<BasicAuthenticationOptions, BasicAuthHandler>(
                   authenticationScheme: scheme,
                   displayName: "Basic Authentication",
                    configureOptions: opts =>
                   {
                       // let caller mutate everything first
                       configure?.Invoke(opts);
                       ConfigureBasicAuthValidators(host, opts);
                       ConfigureBasicIssueClaims(host, opts);
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
    /// <param name="configure">The BasicAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
        this KestrunHost host,
        string scheme,
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
        return host.AddBasicAuthentication(
            scheme: scheme,
            configure: opts =>
            {
                // Copy properties from the provided configure object
                opts.HeaderName = configure.HeaderName;
                opts.Base64Encoded = configure.Base64Encoded;
                if (configure.SeparatorRegex is not null)
                {
                    opts.SeparatorRegex = new Regex(configure.SeparatorRegex.ToString(), configure.SeparatorRegex.Options);
                }

                opts.Realm = configure.Realm;
                opts.RequireHttps = configure.RequireHttps;
                opts.SuppressWwwAuthenticate = configure.SuppressWwwAuthenticate;
                // Logger configuration
                opts.Logger = configure.Logger == host.Logger.ForContext<BasicAuthenticationOptions>() ?
                            host.Logger.ForContext<BasicAuthenticationOptions>() : configure.Logger;

                // Copy properties from the provided configure object
                opts.ValidateCodeSettings = configure.ValidateCodeSettings;
                opts.IssueClaimsCodeSettings = configure.IssueClaimsCodeSettings;

                // Claims policy configuration
                opts.ClaimPolicyConfig = configure.ClaimPolicyConfig;
            }
        );
    }

    #endregion
    #region GitHub OAuth Authentication
    /// <summary>
    /// Adds GitHub OAuth (Authorization Code) authentication with optional email enrichment.
    /// Creates three schemes: <paramref name="scheme"/>, <paramref name="scheme"/>.Cookies, <paramref name="scheme"/>.Policy.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">Base scheme name (e.g. "GitHub").</param>
    /// <param name="clientId">GitHub OAuth App Client ID.</param>
    /// <param name="clientSecret">GitHub OAuth App Client Secret.</param>
    /// <param name="callbackPath">The callback path for OAuth redirection (e.g. "/signin-github").</param>
    /// <returns>The configured KestrunHost.</returns>
    public static KestrunHost AddGitHubOAuthAuthentication(
        this KestrunHost host,
        string scheme,
        string clientId,
        string clientSecret,
        string callbackPath)
    {
        var opts = new OAuth2Options
        {
            ClientId = clientId,
            ClientSecret = clientSecret,

            CallbackPath = callbackPath,

            AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
            TokenEndpoint = "https://github.com/login/oauth/access_token",
            UserInformationEndpoint = "https://api.github.com/user"
        };

        // Request additional scopes if you want (optional)
        opts.Scope.Add("read:user");
        opts.Scope.Add("user:email");

        opts.SaveTokens = true;
        opts.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        // Map JSON from the user endpoint to claims
        opts.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        opts.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        opts.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        opts.ClaimActions.MapJsonKey("name", "name");  // GitHub's display name (may be null)
        opts.ClaimActions.MapJsonKey("urn:github:login", "login");
        opts.ClaimActions.MapJsonKey("urn:github:avatar_url", "avatar_url");
        opts.ClaimActions.MapJsonKey("urn:github:html_url", "html_url");

        opts.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                // Fetch the user info from GitHub
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new("application/json"));
                request.Headers.Add("User-Agent", "KestrunOAuth/1.0");
                request.Headers.Authorization = new("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.HttpContext.RequestAborted);

                _ = response.EnsureSuccessStatusCode();

                using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                context.RunClaimActions(user.RootElement);

                // Enrich email if missing from /user endpoint (requires user:email scope)
                if (context.Identity is not null &&
                    !context.Identity.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    try
                    {
                        var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                        emailRequest.Headers.Accept.Add(new("application/json"));
                        emailRequest.Headers.Add("User-Agent", "KestrunOAuth/1.0");
                        emailRequest.Headers.Authorization = new("Bearer", context.AccessToken);

                        var emailResponse = await context.Backchannel.SendAsync(emailRequest,
                            HttpCompletionOption.ResponseHeadersRead,
                            context.HttpContext.RequestAborted);

                        if (emailResponse.IsSuccessStatusCode)
                        {
                            using var emails = JsonDocument.Parse(await emailResponse.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));

                            // Find primary verified email, fallback to first verified email
                            string? primaryEmail = null;
                            foreach (var emailObj in emails.RootElement.EnumerateArray())
                            {
                                var isPrimary = emailObj.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean();
                                var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();

                                if (isPrimary && isVerified && emailObj.TryGetProperty("email", out var emailProp))
                                {
                                    primaryEmail = emailProp.GetString();
                                    break;
                                }
                            }

                            // Fallback to first verified email if no primary found
                            if (string.IsNullOrWhiteSpace(primaryEmail))
                            {
                                foreach (var emailObj in emails.RootElement.EnumerateArray())
                                {
                                    var isVerified = emailObj.TryGetProperty("verified", out var verifiedProp) && verifiedProp.GetBoolean();
                                    if (isVerified && emailObj.TryGetProperty("email", out var emailProp))
                                    {
                                        primaryEmail = emailProp.GetString();
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(primaryEmail))
                            {
                                context.Identity.AddClaim(new Claim(
                                    ClaimTypes.Email,
                                    primaryEmail,
                                    ClaimValueTypes.String,
                                    context.Options.ClaimsIssuer));
                            }
                        }
                    }
                    catch
                    {
                        if (host.Logger.IsEnabled(LogEventLevel.Verbose))
                        {
                            host.Logger.Verbose("Failed to enrich GitHub email claim.");
                        }
                        // Best-effort email enrichment; ignore failures
                    }
                }
            }
        };
        return host.AddOAuth2Authentication(scheme, opts);
    }


    #endregion
    #region JWT Bearer Authentication
    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host.
    /// <para>Use this for APIs that require token-based authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Bearer").</param>
    /// <param name="validationParameters">Parameters used to validate JWT tokens.</param>
    /// <param name="configureJwt">Optional hook to customize JwtBearerOptions.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
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
      string scheme,
      TokenValidationParameters validationParameters,
      Action<JwtBearerOptions>? configureJwt = null,
      ClaimPolicyConfig? claimPolicy = null)
    {
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "JwtBearer", configureJwt);
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddJwtBearer(scheme, opts =>
                {
                    opts.TokenValidationParameters = validationParameters;
                    opts.MapInboundClaims = true;
                    configureJwt?.Invoke(opts);
                });
            },
            configureAuthz: claimPolicy?.ToAuthzDelegate()
            );
    }
    #endregion
    #region Cookie Authentication
    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host.
    /// <para>Use this for browser-based authentication using cookies.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>
    /// <param name="configure">Optional configuration for CookieAuthenticationOptions.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddCookieAuthentication(
        this KestrunHost host,
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
        Action<CookieAuthenticationOptions>? configure = null,
     ClaimPolicyConfig? claimPolicy = null)
    {
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "Cookie", configure);
        // Add authentication
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddCookie(
                    authenticationScheme: scheme,
                    configureOptions: opts =>
                    {
                        // let caller mutate everything first
                        configure?.Invoke(opts);
                        host.Logger.Debug("Configured Cookie Authentication with LoginPath: {LoginPath}", opts.LoginPath);
                    });
            },
             configureAuthz: claimPolicy?.ToAuthzDelegate()
        );
    }


    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>
    /// <param name="configure">The CookieAuthenticationOptions object to configure the authentication.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddCookieAuthentication(
          this KestrunHost host,
          string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
          CookieAuthenticationOptions? configure = null,
       ClaimPolicyConfig? claimPolicy = null)
    {
        // If no object provided just delegate to action overload without extra config
        return configure is null
            ? host.AddCookieAuthentication(
                scheme: scheme,
                configure: (Action<CookieAuthenticationOptions>?)null,
                claimPolicy: claimPolicy)
            : host.AddCookieAuthentication(
            scheme: scheme,
            configure: opts =>
            {
                // Copy relevant properties from provided options instance to the framework-created one
                CopyCookieAuthenticationOptions(configure, opts);
            },
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
    /// <para>
    /// The authentication scheme name is <see cref="NegotiateDefaults.AuthenticationScheme"/>.
    /// This enables Kerberos and NTLM authentication.
    /// </para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddWindowsAuthentication(this KestrunHost host)
    {
        var options = new AuthenticationSchemeOptions();
        //register in host for introspection
        _ = host.RegisteredAuthentications.Register("Windows", "WindowsAuth", options);

        // Add authentication
        return host.AddAuthentication(
            defaultScheme: NegotiateDefaults.AuthenticationScheme,
            buildSchemes: ab =>
            {
                _ = ab.AddNegotiate();
            }
        );
    }
    #endregion
    #region API Key Authentication
    /// <summary>
    /// Adds API Key Authentication to the Kestrun host.
    /// <para>Use this for endpoints that require an API key for access.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is "ApiKey").</param>
    /// <param name="configure">Optional configuration for ApiKeyAuthenticationOptions.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme = "ApiKey",
    Action<ApiKeyAuthenticationOptions>? configure = null)
    {
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "ApiKey", configure);
        // Add authentication
        var h = host.AddAuthentication(
           defaultScheme: scheme,
           buildSchemes: ab =>
           {
               // ← TOptions == ApiKeyAuthenticationOptions
               //    THandler == ApiKeyAuthHandler
               _ = ab.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthHandler>(
                   authenticationScheme: scheme,
                   displayName: "API Key",
                   configureOptions: opts =>
                   {
                       // let caller mutate everything first
                       configure?.Invoke(opts);
                       ConfigureApiKeyValidators(host, opts);
                       ConfigureApiKeyIssueClaims(host, opts);
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
                              IOptionsMonitor<ApiKeyAuthenticationOptions>>()));
        });
    }
    #endregion

    #region OAuth2 Authentication
    /// <summary>
    /// Adds OAuth2 authentication to the Kestrun host.
    /// <para>Use this for applications that require OAuth2 authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="options">The OAuth2Options to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOAuth2Authentication(
        this KestrunHost host,
        string scheme,
        OAuth2Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("ClientId must be provided in OAuth2Options", nameof(options));
        }
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "OAuth2", options);

        // Add authentication
        return host.AddAuthentication(
            defaultScheme: options.AuthenticationScheme,
            defaultChallengeScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddCookie(options.AuthenticationScheme, cookieOpts =>
                {
                    CopyCookieAuthenticationOptions(options.CookieOptions, cookieOpts);
                });
                _ = ab.AddOAuth(scheme, oauthOpts =>
                {
                    options.ApplyTo(oauthOpts);
                });
            });
    }


    #endregion
    #region OpenID Connect Authentication

    /// <summary>
    /// Adds OpenID Connect authentication to the Kestrun host with private key JWT client assertion.
    /// <para>Use this for applications that require OpenID Connect authentication with client credentials using JWT assertion.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="options">The OpenIdConnectOptions to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOpenIdConnectAuthentication(
           this KestrunHost host, string scheme,
           OidcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("ClientId must be provided in OpenIdConnectOptions", nameof(options));
        }
        var clientId = options.ClientId;
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "Oidc", options);

        // CRITICAL: Register OidcEvents and AssertionService in DI before configuring authentication
        // This is required because EventsType expects these to be available in the service provider
        return host.AddService(services =>
         {
             // Register AssertionService as a singleton with factory to pass clientId and jwkJson
             // Only register if JwkJson is provided (for private_key_jwt authentication)
             if (!string.IsNullOrWhiteSpace(options.JwkJson))
             {
                 services.TryAddSingleton(sp => new AssertionService(clientId, options.JwkJson));
                 // Register OidcEvents as scoped (per-request)
                 services.TryAddScoped<OidcEvents>();
             }
         }).AddAuthentication(
              defaultScheme: CookieAuthenticationDefaults.AuthenticationScheme,
              defaultChallengeScheme: scheme,
              buildSchemes: ab =>
              {
                  _ = ab.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookieOpts =>
                 {
                     // Copy cookie configuration from options.CookieOptions
                     CopyCookieAuthenticationOptions(options.CookieOptions, cookieOpts);
                     host.Logger.Debug("Configured Cookie Authentication for OpenID Connect with SlidingExpiration: {SlidingExpiration}",
                         cookieOpts.SlidingExpiration);
                 });

                  _ = ab.AddOpenIdConnect(scheme, oidcOpts =>
                 {
                     // Copy all properties from the provided options to the framework's options
                     options.ApplyTo(oidcOpts);

                     // Inject private key JWT at code → token step (only if JwkJson is provided)
                     // This will be resolved from DI at runtime
                     if (!string.IsNullOrWhiteSpace(options.JwkJson))
                     {
                         oidcOpts.EventsType = typeof(OidcEvents);
                     }

                     host.Logger.Debug("Configured OpenID Connect with Authority: {Authority}, ClientId: {ClientId}, Scopes: {Scopes}",
                         oidcOpts.Authority, oidcOpts.ClientId, string.Join(", ", oidcOpts.Scope));
                 });
              });
    }

    #endregion
    #region Helper Methods
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


    /// <summary>
    /// Adds API Key Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="configure">The ApiKeyAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme,
    ApiKeyAuthenticationOptions configure)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding API Key Authentication with scheme: {Scheme}", scheme);
        }

        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(configure);
        return host.AddApiKeyAuthentication(
            scheme: scheme,
            configure: opts =>
            {
                // let caller mutate everything first
                opts.ExpectedKey = configure.ExpectedKey;
                opts.HeaderName = configure.HeaderName;
                opts.AdditionalHeaderNames = configure.AdditionalHeaderNames;
                opts.AllowQueryStringFallback = configure.AllowQueryStringFallback;
                // Logger configuration
                opts.Logger = configure.Logger == host.Logger.ForContext<ApiKeyAuthenticationOptions>() ?
                        host.Logger.ForContext<ApiKeyAuthenticationOptions>() : configure.Logger;

                opts.RequireHttps = configure.RequireHttps;
                opts.EmitChallengeHeader = configure.EmitChallengeHeader;
                opts.ChallengeHeaderFormat = configure.ChallengeHeaderFormat;
                opts.ValidateCodeSettings = configure.ValidateCodeSettings;
                // IssueClaimsCodeSettings
                opts.IssueClaimsCodeSettings = configure.IssueClaimsCodeSettings;
                // Claims policy configuration
                opts.ClaimPolicyConfig = configure.ClaimPolicyConfig;
            }
        );
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
    /// Helper to copy values from a user-supplied CookieAuthenticationOptions instance to the instance
    /// created by the framework inside AddCookie(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance.
    /// </summary>
    /// <param name="source">The source options to copy from.</param>
    /// <param name="target">The target options to copy to.</param>
    /// <exception cref="ArgumentNullException">Thrown when source or target is null.</exception>
    /// <remarks>
    /// Only copies primitive properties and references. Does not clone complex objects like CookieBuilder.
    /// </remarks>
    private static void CopyCookieAuthenticationOptions(CookieAuthenticationOptions source, CookieAuthenticationOptions target)
    {
        // Paths & return URL
        target.LoginPath = source.LoginPath;
        target.LogoutPath = source.LogoutPath;
        target.AccessDeniedPath = source.AccessDeniedPath;
        target.ReturnUrlParameter = source.ReturnUrlParameter;

        // Expiration & sliding behavior
        target.ExpireTimeSpan = source.ExpireTimeSpan;
        target.SlidingExpiration = source.SlidingExpiration;

        // Cookie builder settings
        // (Cookie is always non-null; copy primitive settings)
        if (source.Cookie.Name is not null)
        {
            target.Cookie.Name = source.Cookie.Name;
            target.Cookie.Path = source.Cookie.Path;
            target.Cookie.Domain = source.Cookie.Domain;
            target.Cookie.HttpOnly = source.Cookie.HttpOnly;
            target.Cookie.SameSite = source.Cookie.SameSite;
            target.Cookie.SecurePolicy = source.Cookie.SecurePolicy;
            target.Cookie.IsEssential = source.Cookie.IsEssential;
            target.Cookie.MaxAge = source.Cookie.MaxAge;
        }
        // Forwarding
        target.ForwardAuthenticate = source.ForwardAuthenticate;
        target.ForwardChallenge = source.ForwardChallenge;
        target.ForwardDefault = source.ForwardDefault;
        target.ForwardDefaultSelector = source.ForwardDefaultSelector;
        target.ForwardForbid = source.ForwardForbid;
        target.ForwardSignIn = source.ForwardSignIn;
        target.ForwardSignOut = source.ForwardSignOut;

        // Data protection / ticket / session
        target.TicketDataFormat = source.TicketDataFormat;
        target.DataProtectionProvider = source.DataProtectionProvider;
        target.SessionStore = source.SessionStore;

        // Events & issuer
        if (source.Events is not null)
        {
            target.Events = source.Events;
        }
        target.EventsType = source.EventsType;
        target.ClaimsIssuer = source.ClaimsIssuer;
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
