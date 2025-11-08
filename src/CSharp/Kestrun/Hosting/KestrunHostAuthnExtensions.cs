using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Kestrun.Authentication;
using Serilog.Events;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Kestrun.Claims;
using Serilog;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text.Json;


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
                opts.Logger = configure.Logger == Log.ForContext<BasicAuthenticationOptions>() ?
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
        _ = host.RegisteredAuthentications.Register(scheme, "Cookie", configure);

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
                        Log.Debug("Configured Cookie Authentication with LoginPath: {LoginPath}", opts.LoginPath);
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

        _ = host.RegisteredAuthentications.Register("Windows", "WindowsAuth", options);
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
    /// <param name="options">The OAuthOptions to configure the authentication.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOAuth2Authentication(
        this KestrunHost host,
        string scheme,
        OAuthOptions options,
        ClaimPolicyConfig? claimPolicy = null /* optional in practice */)
    {
        // register in host for introspection
        _ = host.RegisteredAuthentications.Register(scheme, "OAuth2", options);
        // Derive cookie/policy schemes
        var cookieScheme = string.IsNullOrWhiteSpace(options.SignInScheme)
            ? scheme + ".Cookies"
            : options.SignInScheme;
        var policyScheme = scheme + ".Policy";

        return host.AddAuthentication(
            defaultScheme: policyScheme,
            buildSchemes: ab =>
            {
                // Ensure there's a cookie scheme to sign into
                _ = ab.AddCookie(cookieScheme);

                // Register OAuth handler with the provided options
                _ = ab.AddOAuth(scheme, o =>
                {
                    // Copy everything from the supplied options
                    Copy(o, options);

                    // Ensure we sign into the cookie scheme we just added
                    o.SignInScheme = cookieScheme;

                    // If the caller forgot, keep tokens by default
                    if (!o.SaveTokens)
                    {
                        o.SaveTokens = true;
                    }

                    // If a UserInformationEndpoint is specified and no custom OnCreatingTicket logic
                    // adds claims, attach a default handler that fetches user info JSON and runs claim actions.
                    var previous = o.Events?.OnCreatingTicket;
                    o.Events ??= new OAuthEvents();
                    o.Events.OnCreatingTicket = async context =>
                    {
                        if (previous is not null)
                        {
                            await previous(context).ConfigureAwait(false);
                        }

                        if (!string.IsNullOrWhiteSpace(context.Options.UserInformationEndpoint))
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                            request.Headers.Accept.ParseAdd("application/json");

                            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
                            _ = response.EnsureSuccessStatusCode();

                            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted).ConfigureAwait(false));
                            context.RunClaimActions(payload.RootElement);
                        }
                    };
                });

                // Policy scheme: auth/sign-in/out → cookies; challenge → OAuth
                _ = ab.AddPolicyScheme(policyScheme, "App Auth", fwd =>
                {
                    fwd.ForwardAuthenticate = cookieScheme;
                    fwd.ForwardSignIn = cookieScheme;
                    fwd.ForwardSignOut = cookieScheme;
                    fwd.ForwardForbid = cookieScheme;
                    fwd.ForwardChallenge = scheme;
                });
            },
            // Apply claim policy if supplied (consistent with other schemes)
            configureAuthz: claimPolicy?.ToAuthzDelegate()
        );

        static void Copy(OAuthOptions dst, OAuthOptions src)
        {
            dst.AuthorizationEndpoint = src.AuthorizationEndpoint;
            dst.TokenEndpoint = src.TokenEndpoint;
            dst.UserInformationEndpoint = src.UserInformationEndpoint;
            dst.ClientId = src.ClientId;
            dst.ClientSecret = src.ClientSecret;
            dst.CallbackPath = src.CallbackPath;
            dst.SaveTokens = src.SaveTokens;
            dst.ClaimsIssuer = src.ClaimsIssuer;
            dst.UsePkce = src.UsePkce;
            dst.Scope.Clear();
            foreach (var s in src.Scope)
            {
                dst.Scope.Add(s);
            }

            // Backchannel & timeout (if provided)
            if (src.BackchannelTimeout != default)
            {
                dst.BackchannelTimeout = src.BackchannelTimeout;
            }
            if (src.Backchannel is not null)
            {
                dst.Backchannel = src.Backchannel;
            }
            if (src.BackchannelHttpHandler is not null)
            {
                dst.BackchannelHttpHandler = src.BackchannelHttpHandler;
            }

            // Claim actions & events (if any) need copying explicitly:
            foreach (var a in src.ClaimActions)
            {
                // Only map json-key actions when the JsonKey is available to avoid passing null
                if (a is Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction jka && !string.IsNullOrEmpty(jka.JsonKey))
                {
                    dst.ClaimActions.MapJsonKey(a.ClaimType, jka.JsonKey);
                }
            }

            if (src.Events is not null)
            {
                dst.Events = src.Events;
            }
        }
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
                opts.Logger = configure.Logger == Log.ForContext<ApiKeyAuthenticationOptions>() ?
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

    /// <summary>
    /// Adds OpenID Connect authentication to the Kestrun host.
    /// <para>Use this for applications that require OpenID Connect authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="clientId">The client ID for the OpenID Connect application.</param>
    /// <param name="clientSecret">The client secret for the OpenID Connect application.</param>
    /// <param name="authority">The authority URL for the OpenID Connect provider.</param>
    /// <param name="configure">An optional action to configure the OpenID Connect options.</param>
    /// <param name="configureAuthz">An optional action to configure the authorization options.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOpenIdConnectAuthentication(
        this KestrunHost host,
        string scheme,
        string clientId,
        string clientSecret,
        string authority,
        Action<OpenIdConnectOptions>? configure = null,
        Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddOpenIdConnect(
                    authenticationScheme: scheme,
                    displayName: "OIDC",
                    configureOptions: opts =>
                    {
                        opts.ClientId = clientId;
                        opts.ClientSecret = clientSecret;
                        opts.Authority = authority;
                        opts.ResponseType = "code";
                        opts.SaveTokens = true;
                        configure?.Invoke(opts);
                    });
            },
            configureAuthz: configureAuthz
        );
    }
    #endregion

    /// <summary>
    /// Adds authentication and authorization middleware to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="buildSchemes">A delegate to configure authentication schemes.</param>
    /// <param name="defaultScheme">The default authentication scheme (default is JwtBearer).</param>
    /// <param name="configureAuthz">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    internal static KestrunHost AddAuthentication(this KestrunHost host,
    Action<AuthenticationBuilder> buildSchemes,            // ← unchanged
    string defaultScheme = JwtBearerDefaults.AuthenticationScheme,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        _ = host.AddService(services =>
        {
            var ab = services.AddAuthentication(defaultScheme);
            buildSchemes(ab);                                  // Basic + JWT here

            // make sure UseAuthorization() can find its services
            _ = configureAuthz is null ? services.AddAuthorization() : services.AddAuthorization(configureAuthz);
        });

        return host.Use(app =>
        {
            const string Key = "__kr.authmw";
            if (!app.Properties.ContainsKey(Key))
            {
                _ = app.UseAuthentication();
                _ = app.UseAuthorization();
                app.Properties[Key] = true;
                Log.Information("Kestrun: Authentication & Authorization middleware added.");
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
        target.Cookie.Name = source.Cookie.Name;
        target.Cookie.Path = source.Cookie.Path;
        target.Cookie.Domain = source.Cookie.Domain;
        target.Cookie.HttpOnly = source.Cookie.HttpOnly;
        target.Cookie.SameSite = source.Cookie.SameSite;
        target.Cookie.SecurePolicy = source.Cookie.SecurePolicy;
        target.Cookie.IsEssential = source.Cookie.IsEssential;
        target.Cookie.MaxAge = source.Cookie.MaxAge;

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
}
