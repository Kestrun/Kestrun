using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.Options;

namespace Kestrun.Authentication;

/// <summary>
/// Handles Client Certificate Authentication for HTTP requests.
/// </summary>
public class ClientCertificateAuthHandler : CertificateAuthenticationHandler, IAuthHandler
{
    private readonly ClientCertificateAuthenticationOptions _certOptions;

    /// <summary>
    /// The Kestrun host instance.
    /// </summary>
    public KestrunHost Host { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCertificateAuthHandler"/> class.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="options">The options for Client Certificate Authentication.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public ClientCertificateAuthHandler(
        KestrunHost host,
        IOptionsMonitor<ClientCertificateAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        _certOptions = options.CurrentValue;

        if (Host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            Host.Logger.Debug("ClientCertificateAuthHandler initialized");
        }
    }

    /// <summary>
    /// Handles the authentication process for Client Certificate Authentication.
    /// </summary>
    /// <returns>A task representing the authentication result.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Call the base implementation which handles certificate validation
            var result = await base.HandleAuthenticateAsync();

            if (!result.Succeeded)
            {
                return result;
            }

            // If we have a custom claims issuer, add those claims
            if (_certOptions.IssueClaims != null && result.Principal?.Identity?.IsAuthenticated == true)
            {
                var clientCert = await Context.Connection.GetClientCertificateAsync();
                if (clientCert != null)
                {
                    var additionalClaims = await _certOptions.IssueClaims(Context, clientCert);
                    if (additionalClaims != null && additionalClaims.Any())
                    {
                        var identity = result.Principal.Identities.First();
                        identity.AddClaims(additionalClaims);

                        var newPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);
                        var newTicket = new AuthenticationTicket(newPrincipal, result.Properties, Scheme.Name);
                        return AuthenticateResult.Success(newTicket);
                    }
                }
            }

            Host.Logger.Information("Client certificate authentication succeeded");
            return result;
        }
        catch (Exception ex)
        {
            Host.Logger.Error(ex, "Error processing Client Certificate Authentication");
            return AuthenticateResult.Fail("Exception during authentication");
        }
    }

    /// <summary>
    /// Handles the challenge response for Client Certificate Authentication.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the forbidden response for Client Certificate Authentication.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds a PowerShell-based claims issuer function.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="settings">The authentication code settings containing the PowerShell script.</param>
    /// <returns>A function that issues claims using the provided PowerShell script.</returns>
    public static Func<HttpContext, X509Certificate2, Task<IEnumerable<System.Security.Claims.Claim>>> BuildPsClaimsIssuer(
        KestrunHost host,
        AuthenticationCodeSettings settings)
    {
        if (host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            host.Logger.Debug("BuildPsClaimsIssuer settings: {Settings}", settings);
        }

        return async (ctx, cert) =>
        {
            var claims = await IAuthHandler.IssuePowerShellClaimsAsync(
                settings.Code,
                ctx,
                new Dictionary<string, object?>
                {
                    { "certificate", cert },
                    { "thumbprint", cert.Thumbprint },
                    { "subject", cert.Subject },
                    { "issuer", cert.Issuer }
                },
                host.Logger);
            return claims;
        };
    }

    /// <summary>
    /// Builds a C#-based claims issuer function.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="settings">The authentication code settings containing the C# script.</param>
    /// <returns>A function that issues claims using the provided C# script.</returns>
    public static Func<HttpContext, X509Certificate2, Task<IEnumerable<System.Security.Claims.Claim>>> BuildCsClaimsIssuer(
        KestrunHost host,
        AuthenticationCodeSettings settings)
    {
        if (host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            host.Logger.Debug("BuildCsClaimsIssuer settings: {Settings}", settings);
        }

        var core = IAuthHandler.BuildCsClaimsIssuer(
            host,
            settings,
            ("certificate", typeof(X509Certificate2)),
            ("thumbprint", string.Empty),
            ("subject", string.Empty),
            ("issuer", string.Empty)
        ) ?? throw new InvalidOperationException("Failed to build C# claims issuer delegate from provided settings.");

        return (ctx, cert) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["certificate"] = cert,
                ["thumbprint"] = cert.Thumbprint,
                ["subject"] = cert.Subject,
                ["issuer"] = cert.Issuer
            });
    }

    /// <summary>
    /// Builds a VB.NET-based claims issuer function.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="settings">The authentication code settings containing the VB.NET script.</param>
    /// <returns>A function that issues claims using the provided VB.NET script.</returns>
    public static Func<HttpContext, X509Certificate2, Task<IEnumerable<System.Security.Claims.Claim>>> BuildVBNetClaimsIssuer(
        KestrunHost host,
        AuthenticationCodeSettings settings)
    {
        if (host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            host.Logger.Debug("BuildVBNetClaimsIssuer settings: {Settings}", settings);
        }

        var core = IAuthHandler.BuildVBNetClaimsIssuer(
            host,
            settings,
            ("certificate", typeof(X509Certificate2)),
            ("thumbprint", string.Empty),
            ("subject", string.Empty),
            ("issuer", string.Empty)
        ) ?? throw new InvalidOperationException("Failed to build VB.NET claims issuer delegate from provided settings.");

        return (ctx, cert) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["certificate"] = cert,
                ["thumbprint"] = cert.Thumbprint,
                ["subject"] = cert.Subject,
                ["issuer"] = cert.Issuer
            });
    }
}
