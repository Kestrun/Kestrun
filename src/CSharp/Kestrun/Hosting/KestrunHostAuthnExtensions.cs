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

using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;

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
    /// <param name="scope">Optional additional scopes (default adds read:user and user:email).</param>
    /// <param name="callbackPath">Optional callback path (default "/signin-github"). Must match the OAuth App's configured redirect URI path.</param>
    /// <param name="enrichEmail">Fetch /user/emails and add ClaimTypes.Email if missing (requires user:email scope).</param>
    /// <param name="configure">Optional mutation of OAuthOptions before handler build.</param>
    /// <returns>The configured KestrunHost.</returns>
    public static KestrunHost AddGitHubOAuthAuthentication(
        this KestrunHost host,
        string scheme,
        string clientId,
        string clientSecret,
        IEnumerable<string>? scope = null,
        string? callbackPath = null,
        bool enrichEmail = true,
        Action<OAuthOptions>? configure = null)
    {
        var opts = new OAuthOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
            TokenEndpoint = "https://github.com/login/oauth/access_token",
            CallbackPath = "/signin-github",
            UserInformationEndpoint = "https://api.github.com/user",
            ClaimsIssuer = "github.com",
            UsePkce = true,
            SaveTokens = true
        };
        if (!string.IsNullOrWhiteSpace(callbackPath))
        {
            // Normalize to leading '/'
            opts.CallbackPath = callbackPath!.StartsWith("/") ? callbackPath : "/" + callbackPath;
        }
        // Default scopes
        opts.Scope.Clear();
        opts.Scope.Add("read:user");
        opts.Scope.Add("user:email");
        if (scope is not null)
        {
            foreach (var s in scope)
            {
                if (!opts.Scope.Contains(s))
                {
                    opts.Scope.Add(s);
                }
            }
        }
        // Map common fields -> claims
        opts.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Name, "login");
        opts.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
        opts.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                // Basic user info
                using var userReq = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                userReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                userReq.Headers.Accept.ParseAdd("application/json");
                try { userReq.Headers.UserAgent.ParseAdd("KestrunOAuth/1.0"); } catch { }
                using var userResp = await context.Backchannel.SendAsync(userReq, context.HttpContext.RequestAborted).ConfigureAwait(false);
                if (userResp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(context.HttpContext.RequestAborted).ConfigureAwait(false));
                    context.RunClaimActions(doc.RootElement);
                }
                // Optional email enrichment
                if (enrichEmail && context.Identity?.Claims?.Any(c => c.Type == System.Security.Claims.ClaimTypes.Email) != true &&
                    context.Options.Scope.Any(s => string.Equals(s, "user:email", StringComparison.OrdinalIgnoreCase)))
                {
                    using var emailReq = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                    emailReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                    emailReq.Headers.Accept.ParseAdd("application/json");
                    try { emailReq.Headers.UserAgent.ParseAdd("KestrunOAuth/1.0"); } catch { }
                    using var emailResp = await context.Backchannel.SendAsync(emailReq, context.HttpContext.RequestAborted).ConfigureAwait(false);
                    if (emailResp.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(await emailResp.Content.ReadAsStringAsync(context.HttpContext.RequestAborted).ConfigureAwait(false));
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement? pick = null;
                            foreach (var e in doc.RootElement.EnumerateArray())
                            {
                                if (e.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True &&
                                    e.TryGetProperty("verified", out var v) && v.ValueKind == JsonValueKind.True)
                                { pick = e; break; }
                            }
                            if (pick is null)
                            {
                                foreach (var e in doc.RootElement.EnumerateArray())
                                {
                                    if (e.TryGetProperty("verified", out var v) && v.ValueKind == JsonValueKind.True) { pick = e; break; }
                                }
                            }
                            if (pick is null && doc.RootElement.GetArrayLength() > 0)
                            {
                                pick = doc.RootElement[0];
                            }
                            if (pick is not null && pick.Value.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
                            {
                                var email = emailProp.GetString();
                                if (!string.IsNullOrWhiteSpace(email))
                                {
                                    context.Identity!.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email!, System.Security.Claims.ClaimValueTypes.String, context.Options.ClaimsIssuer));
                                }
                            }
                        }
                    }
                }
            }
        };
        configure?.Invoke(opts);
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
            defaultChallengeScheme: scheme,
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
                            // Add a default User-Agent if none is present (some providers like GitHub require it)
                            try { request.Headers.UserAgent.ParseAdd("KestrunOAuth/1.0"); } catch { /* ignore */ }

                            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                            {
                                host.Logger.Warning("OAuth userinfo request failed: {StatusCode} {Reason}", (int)response.StatusCode, response.ReasonPhrase);
                                return; // proceed without userinfo claims
                            }

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
    #region OpenID Connect Authentication
    /// <summary>
    /// Adds OpenID Connect authentication to the Kestrun host using simple parameters.
    /// <para>Use this for applications that require OpenID Connect authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="options">The OpenIdConnectOptions to configure the authentication.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddOpenIdConnectAuthentication(
        this KestrunHost host,
        string scheme,
        OpenIdConnectOptions options,
        ClaimPolicyConfig? claimPolicy = null)
    {
        // CRITICAL: Register authentication in the host's registration tracker to prevent duplicate registrations
        // var options = new OpenIdConnectOptions { Authority = authority, ClientId = clientId, ClientSecret = clientSecret };
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding OpenID Connect Authentication with scheme: {Scheme}, Authority: {Authority}, ClientId: {ClientId}",
                scheme, options.Authority, options.ClientId);
        }
        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new ArgumentException("Authority must be provided in OpenIdConnectOptions", nameof(options));
        }
        // Ensure the scheme is not null
        var authority = options.Authority;
        var clientId = options.ClientId;
        var clientSecret = options.ClientSecret;
        var scopes = options.Scope;
        _ = host.RegisteredAuthentications.Register(scheme, "OpenIdConnect", options);

        var h = host.AddAuthentication(
            defaultScheme: CookieAuthenticationDefaults.AuthenticationScheme,
            defaultChallengeScheme: scheme,
            buildSchemes: ab =>
            {
                _ = ab.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opts =>
                {
                    opts.SlidingExpiration = true;
                    host.Logger.Debug("Configured Cookie Authentication for OpenID Connect");
                });

                _ = ab.AddOpenIdConnect(scheme, opts =>
                {
                    opts.Authority = authority.TrimEnd('/');
                    opts.ClientId = clientId;
                    if (!string.IsNullOrWhiteSpace(clientSecret))
                    {
                        opts.ClientSecret = clientSecret;
                    }

                    // CRITICAL: Create backchannel immediately during configuration
                    // The framework's OpenIdConnectPostConfigureOptions checks if Backchannel is null
                    // and creates one using BackchannelHttpHandler. However:
                    // 1. We can't set BackchannelHttpHandler to a DelegatingHandler (framework will fail to recreate clients)
                    // 2. We can't leave Backchannel null (framework creates one without BaseAddress)
                    // Solution: Create Backchannel with BaseAddress set, and DON'T set BackchannelHttpHandler

                    // Create a logging handler to intercept HTTP requests
                    var loggingHandler = new HttpClientHandler();
                    var loggingMessageHandler = new LoggingHttpMessageHandler(loggingHandler, host.Logger);

                    // Create HttpClient with BaseAddress set to the authority
                    // This prevents "invalid request URI" errors when making token requests
                    opts.Backchannel = new HttpClient(loggingMessageHandler)
                    {
                        BaseAddress = new Uri(authority),
                        Timeout = TimeSpan.FromSeconds(60),
                        MaxResponseContentBufferSize = 10 * 1024 * 1024
                    };

                    // DO NOT set BackchannelHttpHandler - it will cause the framework to recreate
                    // the client and lose our logging handler and BaseAddress
                    host.Logger.Debug($"Created backchannel HttpClient with BaseAddress={authority} and logging handler");

                    // Scopes
                    opts.Scope.Clear();
                    opts.Scope.Add("openid");
                    opts.Scope.Add("profile");
                    if (scopes != null)
                    {
                        foreach (var scope in scopes)
                        {
                            if (!string.IsNullOrWhiteSpace(scope) && !opts.Scope.Contains(scope))
                            {
                                opts.Scope.Add(scope);
                            }
                        }
                    }

                    // Flow configuration
                    opts.ResponseType = options.ResponseType ?? OpenIdConnectResponseType.Code;
                    opts.ResponseMode = options.ResponseMode ?? OpenIdConnectResponseMode.FormPost;
                    opts.UsePkce = options.UsePkce;
                    // Ensure token persistence and userinfo retrieval default to true when requested
                    opts.SaveTokens = options.SaveTokens;
                    opts.GetClaimsFromUserInfoEndpoint = options.GetClaimsFromUserInfoEndpoint;
                    opts.MapInboundClaims = options.MapInboundClaims;

                    // Copy claim mappings from provided options (JsonKey -> ClaimType), avoiding duplicates
                    try
                    {
                        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var x in opts.ClaimActions)
                        {
                            if (x is Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction j && !string.IsNullOrEmpty(j.JsonKey) && !string.IsNullOrEmpty(x.ClaimType))
                            {
                                _ = existing.Add($"{x.ClaimType}|{j.JsonKey}");
                            }
                        }

                        if (options.ClaimActions is not null)
                        {
                            foreach (var a in options.ClaimActions)
                            {
                                if (a is Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction jka &&
                                    !string.IsNullOrEmpty(jka.JsonKey) && !string.IsNullOrEmpty(a.ClaimType))
                                {
                                    var key = $"{a.ClaimType}|{jka.JsonKey}";
                                    if (!existing.Contains(key))
                                    {
                                        opts.ClaimActions.MapJsonKey(a.ClaimType, jka.JsonKey);
                                        _ = existing.Add(key);
                                    }
                                }
                            }
                        }

                        // Add sensible defaults if not already present
                        void AddDefault(string claimType, string jsonKey)
                        {
                            var key = $"{claimType}|{jsonKey}";
                            if (!existing.Contains(key))
                            {
                                opts.ClaimActions.MapJsonKey(claimType, jsonKey);
                                _ = existing.Add(key);
                            }
                        }
                        AddDefault("email", "email");
                        AddDefault("name", "name");
                        AddDefault("preferred_username", "preferred_username");
                        AddDefault("given_name", "given_name");
                        AddDefault("family_name", "family_name");
                    }
                    catch (Exception ex)
                    {
                        host.Logger.Error(ex, "Failed to apply OIDC claim action mappings");
                    }
                    opts.SignedOutRedirectUri = options.SignedOutRedirectUri;
                    opts.SignedOutCallbackPath = options.SignedOutCallbackPath;
                    // Protocol validation - use framework defaults
                    // The default ProtocolValidator handles token response validation correctly
                    // for different flows and response types

                    // Token validation
                    opts.TokenValidationParameters.NameClaimType = "name";
                    opts.TokenValidationParameters.RoleClaimType = "roles";

                    // Sign-in linkage
                    opts.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    opts.SignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                    // Merge user-provided events (if any) with our diagnostics/backchannel fixes
                    var userEvents = options.Events;
                    opts.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            // Invoke user handler first
                            if (userEvents?.OnRedirectToIdentityProvider != null)
                            {
                                var t = userEvents.OnRedirectToIdentityProvider(context);
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return t;
                                }
                            }
                            // CRITICAL: Ensure scopes are in the authorization request
                            // This affects PAR (Pushed Authorization Requests) which is what Duende uses
                            host.Logger.Warning($"OnRedirectToIdentityProvider: Scopes in options: {string.Join(", ", context.Options.Scope)}");
                            host.Logger.Warning($"OnRedirectToIdentityProvider: ProtocolMessage.Scope BEFORE: '{context.ProtocolMessage.Scope}'");

                            if (string.IsNullOrEmpty(context.ProtocolMessage.Scope) && context.Options.Scope.Count > 0)
                            {
                                context.ProtocolMessage.Scope = string.Join(" ", context.Options.Scope);
                                host.Logger.Warning($"Set ProtocolMessage.Scope to: '{context.ProtocolMessage.Scope}'");
                            }

                            host.Logger.Warning($"OnRedirectToIdentityProvider: ProtocolMessage.Scope AFTER: '{context.ProtocolMessage.Scope}'");
                            host.Logger.Warning($"Authorization request: ResponseType={context.ProtocolMessage.ResponseType}, RedirectUri={context.ProtocolMessage.RedirectUri}");

                            return Task.CompletedTask;
                        },
                        OnAuthorizationCodeReceived = context =>
                        {
                            // Invoke user handler first (e.g., to inject private_key_jwt assertion)
                            if (userEvents?.OnAuthorizationCodeReceived != null)
                            {
                                var t = userEvents.OnAuthorizationCodeReceived(context);
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return t;
                                }
                            }
                            var req = context.TokenEndpointRequest;
                            var codePreview = req?.Code?.Length > 10 ? req.Code[..10] : req?.Code;

                            // CRITICAL DEBUG: Check configuration state at token redemption time
                            var config = context.Options.Configuration;
                            host.Logger.Warning($"OnAuthCodeReceived: Config={config != null}, TokenEndpoint={config?.TokenEndpoint}, Backchannel={context.Options.Backchannel != null}, SigningKeys={config?.SigningKeys?.Count ?? 0}");

                            // CRITICAL FIX: Ensure signing keys are loaded before token validation
                            if (config != null && (config.SigningKeys == null || config.SigningKeys.Count == 0) && !string.IsNullOrEmpty(config.JwksUri))
                            {
                                host.Logger.Warning($"SigningKeys missing in OnAuthCodeReceived, fetching from: {config.JwksUri}");
                                try
                                {
                                    var docRetriever = new HttpDocumentRetriever(context.Options.Backchannel ?? new HttpClient());
                                    var jwksJson = docRetriever.GetDocumentAsync(config.JwksUri, CancellationToken.None).GetAwaiter().GetResult();
                                    var jwks = JsonWebKeySet.Create(jwksJson);

                                    if (config.SigningKeys != null && jwks.Keys.Count > 0)
                                    {
                                        foreach (var key in jwks.Keys)
                                        {
                                            config.SigningKeys.Add(key);
                                        }
                                        host.Logger.Warning($"Added {jwks.Keys.Count} signing keys from JWKS endpoint");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    host.Logger.Error(ex, "Failed to fetch signing keys in OnAuthCodeReceived");
                                }
                            }

                            host.Logger.Debug("Authorization code received. RedirectUri={RedirectUri}, ClientId={ClientId}, HasSecret={HasSecret}, Code={Code}",
                                req?.RedirectUri, req?.ClientId, !string.IsNullOrEmpty(req?.ClientSecret), codePreview);

                            // CRITICAL FIX: Manually add scope parameter to token request
                            // The framework should do this automatically but it's not happening
                            if (req != null)
                            {
                                host.Logger.Warning($"Token endpoint request BEFORE scope fix: GrantType={req.GrantType}, Scope='{req.Scope}', RedirectUri={req.RedirectUri}");

                                if (string.IsNullOrEmpty(req.Scope) && context.Options.Scope.Count > 0)
                                {
                                    var scopeValue = string.Join(" ", context.Options.Scope);
                                    req.SetParameter("scope", scopeValue);
                                    host.Logger.Warning($"Used SetParameter to add scope: '{scopeValue}'");
                                    host.Logger.Warning($"Token endpoint request AFTER scope fix: GrantType={req.GrantType}, Scope='{req.Scope}', GetParameter('scope')='{req.GetParameter("scope")}'");
                                }
                            }

                            // NOTE: Token redemption happens AFTER this event returns
                            // We can't access the token response here yet
                            return Task.CompletedTask;
                        },
                        OnTokenResponseReceived = context =>
                        {
                            if (userEvents?.OnTokenResponseReceived != null)
                            {
                                var t = userEvents.OnTokenResponseReceived(context);
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return t;
                                }
                            }
                            var tokenResponse = context.TokenEndpointResponse;
                            host.Logger.Warning($"Token response: HasIdToken={!string.IsNullOrEmpty(tokenResponse?.IdToken)}, HasAccessToken={!string.IsNullOrEmpty(tokenResponse?.AccessToken)}, HasRefreshToken={!string.IsNullOrEmpty(tokenResponse?.RefreshToken)}");

                            // CRITICAL FIX: access_token is in the HTTP response but not parsed into Parameters/AccessToken property
                            // This happens because the response body from LoggingHttpMessageHandler is stored there
                            // We need to extract it from our handler's storage
                            if (tokenResponse != null && string.IsNullOrEmpty(tokenResponse.AccessToken))
                            {
                                // Check if our logging handler captured the response body
                                var capturedBody = LoggingHttpMessageHandler.LastTokenResponseBody;
                                if (!string.IsNullOrEmpty(capturedBody))
                                {
                                    try
                                    {
                                        // Parse the JSON and extract access_token
                                        using var jsonDoc = JsonDocument.Parse(capturedBody);
                                        if (jsonDoc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
                                        {
                                            var accessToken = accessTokenElement.GetString();
                                            if (!string.IsNullOrEmpty(accessToken))
                                            {
                                                tokenResponse.AccessToken = accessToken;
                                                tokenResponse.SetParameter("access_token", accessToken);
                                                host.Logger.Warning($"MANUALLY EXTRACTED access_token from captured response body: {accessToken[..Math.Min(30, accessToken.Length)]}...");
                                            }
                                        }

                                        // Also extract token_type if missing
                                        if (string.IsNullOrEmpty(tokenResponse.TokenType) && jsonDoc.RootElement.TryGetProperty("token_type", out var tokenTypeElement))
                                        {
                                            tokenResponse.TokenType = tokenTypeElement.GetString();
                                        }

                                        // Extract scope if missing
                                        if (jsonDoc.RootElement.TryGetProperty("scope", out var scopeElement))
                                        {
                                            tokenResponse.Scope = scopeElement.GetString();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        host.Logger.Error(ex, "Failed to manually parse token response from captured body");
                                    }
                                }
                                else
                                {
                                    host.Logger.Error("CRITICAL: access_token missing and no captured response body available!");
                                }
                            }

                            host.Logger.Warning($"After manual extraction: HasAccessToken={!string.IsNullOrEmpty(tokenResponse?.AccessToken)}");
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            if (userEvents?.OnTokenValidated != null)
                            {
                                var t = userEvents.OnTokenValidated(context);
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return t;
                                }
                            }
                            // CRITICAL FIX: Manually extract access_token from the token response if it's missing
                            // This event fires AFTER token response parsing but BEFORE protocol validation
                            var tokenResponse = context.TokenEndpointResponse;

                            if (tokenResponse != null && string.IsNullOrEmpty(tokenResponse.AccessToken))
                            {
                                // Try to manually parse from Parameters one more time
                                if (tokenResponse.Parameters != null && tokenResponse.Parameters.TryGetValue("access_token", out var accessTokenObj))
                                {
                                    var accessToken = accessTokenObj?.ToString();
                                    if (!string.IsNullOrEmpty(accessToken))
                                    {
                                        tokenResponse.AccessToken = accessToken;
                                        host.Logger.Warning($"OnTokenValidated: Extracted access_token: {accessToken[..Math.Min(30, accessToken.Length)]}...");
                                    }
                                }
                            }

                            // Fallback: fetch UserInfo if profile claims missing
                            try
                            {
                                var principal = context.Principal;
                                var identity = principal?.Identity as System.Security.Claims.ClaimsIdentity;
                                var hasProfile = identity?.Claims?.Any(c => c.Type is "email" or "name" or "preferred_username") == true;
                                var accessToken = tokenResponse?.AccessToken;
                                var userInfoEndpoint = context.Options.Configuration?.UserInfoEndpoint;
                                if (!hasProfile && !string.IsNullOrEmpty(accessToken) && context.Options.GetClaimsFromUserInfoEndpoint)
                                {
                                    if (string.IsNullOrEmpty(userInfoEndpoint) && !string.IsNullOrEmpty(context.Options.Authority))
                                    {
                                        userInfoEndpoint = $"{context.Options.Authority.TrimEnd('/')}/connect/userinfo";
                                    }
                                    if (!string.IsNullOrEmpty(userInfoEndpoint) && identity is not null)
                                    {
                                        var req = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
                                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                                        var http = context.Options.Backchannel ?? new HttpClient();
                                        var resp = http.SendAsync(req, CancellationToken.None).GetAwaiter().GetResult();
                                        if (resp.IsSuccessStatusCode)
                                        {
                                            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                                            using var doc = JsonDocument.Parse(json);
                                            var root = doc.RootElement;
                                            if (root.TryGetProperty("email", out var emailEl))
                                            {
                                                identity.AddClaim(new System.Security.Claims.Claim("email", emailEl.GetString() ?? string.Empty));
                                            }
                                            if (root.TryGetProperty("name", out var nameEl))
                                            {
                                                identity.AddClaim(new System.Security.Claims.Claim("name", nameEl.GetString() ?? string.Empty));
                                            }
                                            if (root.TryGetProperty("preferred_username", out var unameEl))
                                            {
                                                identity.AddClaim(new System.Security.Claims.Claim("preferred_username", unameEl.GetString() ?? string.Empty));
                                            }
                                            if (root.TryGetProperty("given_name", out var givenEl))
                                            {
                                                identity.AddClaim(new System.Security.Claims.Claim("given_name", givenEl.GetString() ?? string.Empty));
                                            }
                                            if (root.TryGetProperty("family_name", out var famEl))
                                            {
                                                identity.AddClaim(new System.Security.Claims.Claim("family_name", famEl.GetString() ?? string.Empty));
                                            }
                                            host.Logger.Warning("Merged UserInfo claims after token validation from {Endpoint}", userInfoEndpoint);
                                        }
                                        else
                                        {
                                            host.Logger.Error("UserInfo call failed after token validation: {StatusCode}", resp.StatusCode);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                host.Logger.Error(ex, "UserInfo fallback failed in OnTokenValidated");
                            }

                            // De-duplicate across ALL identities: rebuild a single identity with distinct (Type,Value)
                            try
                            {
                                var principal = context.Principal;
                                if (principal is not null)
                                {
                                    var allClaims = principal.Identities.SelectMany(i => i.Claims).ToList();
                                    var distinctClaims = allClaims
                                        .GroupBy(c => $"{c.Type}|{c.Value}", StringComparer.OrdinalIgnoreCase)
                                        .Select(g => g.First())
                                        .ToList();

                                    var first = principal.Identities.FirstOrDefault();
                                    var authType = first?.AuthenticationType;
                                    var nameClaimType = first?.NameClaimType ?? "name";
                                    var roleClaimType = first?.RoleClaimType ?? "roles";
                                    if (distinctClaims.Count != allClaims.Count)
                                    {
                                        var newIdentity = new System.Security.Claims.ClaimsIdentity(distinctClaims, authType, nameClaimType, roleClaimType);
                                        context.Principal = new System.Security.Claims.ClaimsPrincipal(newIdentity);
                                        host.Logger.Warning("Deduplicated claims: removed {Removed} duplicates (final {Final} of original {Original})", allClaims.Count - distinctClaims.Count, distinctClaims.Count, allClaims.Count);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                host.Logger.Error(ex, "Failed to globally de-duplicate claims");
                            }

                            host.Logger.Debug("Token validated successfully (profile claims dedup applied)");
                            return Task.CompletedTask;
                        },
                        OnRemoteFailure = context =>
                        {
                            if (userEvents?.OnRemoteFailure != null)
                            {
                                var t = userEvents.OnRemoteFailure(context);
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return t;
                                }
                            }
                            host.Logger.Error("Remote authentication failed: {ErrorMessage}", context.Failure?.Message);
                            return Task.CompletedTask;
                        }
                    };

                    host.Logger.Information("Configured OpenID Connect: Authority={Authority}, ClientId={ClientId}",
                        opts.Authority, opts.ClientId);
                });
            },
            configureAuthz: claimPolicy?.ToAuthzDelegate()
        );

        // CRITICAL FIX: Register PostConfigure AFTER AddAuthentication to fix framework bug
        // This runs after the OIDC handler is registered and can fix the broken backchannel
        return h.AddService(services =>
        {
            _ = services.PostConfigure<OpenIdConnectOptions>(scheme, opts =>
            {
                host.Logger.Warning($"PostConfigure {scheme}: Backchannel={opts.Backchannel != null}, Handler={opts.BackchannelHttpHandler != null}, Config={opts.Configuration != null}, ConfigMgr={opts.ConfigurationManager != null}");

                // CRITICAL FIX: Configuration is loaded lazily, but we need it NOW
                // Force the configuration to be fetched synchronously so token endpoint URL is available
                if (opts.Configuration == null && opts.ConfigurationManager != null)
                {
                    try
                    {
                        host.Logger.Warning($"Configuration is NULL but ConfigMgr exists - forcing load for {scheme}");

                        // Use ConfigurationManager to fetch the discovery document
                        // This uses the framework's proper retrieval mechanism with the Backchannel HttpClient
                        var config = opts.ConfigurationManager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();

                        if (config != null)
                        {
                            host.Logger.Warning($"ConfigMgr returned config: Issuer={config.Issuer}, AuthEndpoint={config.AuthorizationEndpoint}, TokenEndpoint={config.TokenEndpoint}, JwksUri={config.JwksUri}, SigningKeysCount={config.SigningKeys?.Count ?? 0}");

                            // WORKAROUND: TokenEndpoint, JwksUri, and EndSessionEndpoint properties are mysteriously empty even though discovery document contains them
                            // Manually construct them from Issuer if needed
                            if (string.IsNullOrEmpty(config.TokenEndpoint) && !string.IsNullOrEmpty(config.Issuer))
                            {
                                config.TokenEndpoint = $"{config.Issuer.TrimEnd('/')}/connect/token";
                                host.Logger.Warning($"TokenEndpoint was empty, manually set to: {config.TokenEndpoint}");
                            }

                            if (string.IsNullOrEmpty(config.JwksUri) && !string.IsNullOrEmpty(config.Issuer))
                            {
                                config.JwksUri = $"{config.Issuer.TrimEnd('/')}/.well-known/openid-configuration/jwks";
                                host.Logger.Warning($"JwksUri was empty, manually set to: {config.JwksUri}");
                            }

                            if (string.IsNullOrEmpty(config.EndSessionEndpoint) && !string.IsNullOrEmpty(config.Issuer))
                            {
                                config.EndSessionEndpoint = $"{config.Issuer.TrimEnd('/')}/connect/endsession";
                                host.Logger.Warning($"EndSessionEndpoint was empty, manually set to: {config.EndSessionEndpoint}");
                            }

                            // CRITICAL: Also check if signing keys are missing and fetch them if needed
                            if ((config.SigningKeys == null || config.SigningKeys.Count == 0) && !string.IsNullOrEmpty(config.JwksUri))
                            {
                                host.Logger.Warning($"SigningKeys are empty, fetching from JwksUri: {config.JwksUri}");
                                try
                                {
                                    // Fetch the JWKS document
                                    var docRetriever = new HttpDocumentRetriever(opts.Backchannel ?? new HttpClient());
                                    var jwksJson = docRetriever.GetDocumentAsync(config.JwksUri, CancellationToken.None).GetAwaiter().GetResult();
                                    var jwks = JsonWebKeySet.Create(jwksJson);

                                    if (config.SigningKeys != null)
                                    {
                                        foreach (var key in jwks.Keys)
                                        {
                                            config.SigningKeys.Add(key);
                                        }
                                        host.Logger.Warning($"Fetched and added {jwks.Keys.Count} signing keys");
                                    }
                                    else
                                    {
                                        host.Logger.Error($"config.SigningKeys is null, cannot add keys for {scheme}");
                                    }
                                }
                                catch (Exception jwksEx)
                                {
                                    host.Logger.Error(jwksEx, $"Failed to fetch signing keys from JwksUri for {scheme}");
                                }
                            }

                            if (!string.IsNullOrEmpty(config.TokenEndpoint))
                            {
                                opts.Configuration = config;
                                host.Logger.Warning($"Configuration assigned successfully. TokenEndpoint={opts.Configuration.TokenEndpoint}, SigningKeys={opts.Configuration.SigningKeys?.Count ?? 0}");
                            }
                            else
                            {
                                host.Logger.Error($"TokenEndpoint is still NULL/empty for {scheme}");
                            }
                        }
                        else
                        {
                            host.Logger.Error($"ConfigurationManager returned null config for {scheme}");
                        }
                    }
                    catch (Exception ex)
                    {
                        host.Logger.Error(ex, $"Failed to load OIDC configuration for {scheme}");
                    }
                }

                // The real issue: Configuration isn't loaded yet, so token endpoint URL is missing
                // Force configuration manager to be created if it doesn't exist
                if (opts.ConfigurationManager == null && !string.IsNullOrEmpty(opts.Authority))
                {
                    host.Logger.Warning($"ConfigurationManager is NULL for {scheme} - creating one");
                    opts.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        $"{opts.Authority.TrimEnd('/')}/.well-known/openid-configuration",
                        new OpenIdConnectConfigurationRetriever(),
                        new HttpDocumentRetriever(opts.Backchannel ?? new HttpClient())
                    );

                    // Try to load configuration immediately
                    try
                    {
                        opts.Configuration = opts.ConfigurationManager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
                        host.Logger.Warning($"Created ConfigMgr and loaded config. TokenEndpoint={opts.Configuration?.TokenEndpoint}");
                    }
                    catch (Exception ex)
                    {
                        host.Logger.Error(ex, $"Failed to load newly created OIDC configuration for {scheme}");
                    }
                }

                if (opts.Backchannel != null && opts.BackchannelHttpHandler == null)
                {
                    // Framework created a broken HttpClient without a handler - fix it
                    host.Logger.Warning($"DETECTED BROKEN BACKCHANNEL for {scheme} - fixing");
                    opts.BackchannelHttpHandler = new HttpClientHandler();
                    opts.Backchannel = new HttpClient(opts.BackchannelHttpHandler)
                    {
                        Timeout = opts.Backchannel.Timeout,
                        MaxResponseContentBufferSize = 10 * 1024 * 1024
                    };
                }
                else if (opts.Backchannel == null)
                {
                    host.Logger.Warning($"Backchannel is NULL for {scheme} - creating");
                    opts.BackchannelHttpHandler = new HttpClientHandler();
                    opts.Backchannel = new HttpClient(opts.BackchannelHttpHandler)
                    {
                        Timeout = TimeSpan.FromSeconds(60),
                        MaxResponseContentBufferSize = 10 * 1024 * 1024
                    };
                }
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
    Action<AuthenticationBuilder> buildSchemes,   // e.g., ab => ab.AddCookie().AddOpenIdConnect("oidc", ...)
    string defaultScheme,
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
            // Configure defaults in one place
            var authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = defaultScheme;
                options.DefaultChallengeScheme = defaultChallengeScheme ?? defaultScheme;
            });

            // Let caller add handlers/schemes
            buildSchemes(authBuilder);

            // Ensure Authorization is available (with optional customization)
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
