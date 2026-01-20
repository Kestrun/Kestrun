using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Kestrun.Authentication;

/// <summary>
/// Handles Client Certificate Authentication for HTTP requests.
/// </summary>
public class ClientCertificateAuthHandler : AuthenticationHandler<ClientCertificateAuthenticationOptions>, IAuthHandler
{
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
            // Get the client certificate
            var clientCertificate = await Context.Connection.GetClientCertificateAsync();

            if (clientCertificate == null)
            {
                Host.Logger.Warning("No client certificate provided");
                return AuthenticateResult.NoResult();
            }

            // Validate the certificate using built-in validation
            var certificateValidator = new CertificateValidator(Options);
            var validationResult = await certificateValidator.ValidateAsync(clientCertificate);

            if (!validationResult.Success)
            {
                Host.Logger.Warning("Certificate validation failed: {Reason}", validationResult.FailureMessage);
                return AuthenticateResult.Fail(validationResult.FailureMessage ?? "Certificate validation failed");
            }

            // Build the claims identity
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, clientCertificate.Subject, ClaimValueTypes.String, Options.ClaimsIssuer),
                new Claim(ClaimTypes.Name, clientCertificate.Subject, ClaimValueTypes.String, Options.ClaimsIssuer)
            };

            // Add thumbprint as a claim
            claims.Add(new Claim("thumbprint", clientCertificate.Thumbprint, ClaimValueTypes.String, Options.ClaimsIssuer));

            // Add issuer and serial number
            claims.Add(new Claim("issuer", clientCertificate.Issuer, ClaimValueTypes.String, Options.ClaimsIssuer));
            claims.Add(new Claim("serialnumber", clientCertificate.SerialNumber ?? string.Empty, ClaimValueTypes.String, Options.ClaimsIssuer));

            // Create identity and principal
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);

            // Invoke the OnAuthenticationSucceeded event if configured
            if (Options.Events is CertificateAuthenticationEvents certEvents)
            {
                var certValidatedContext = new CertificateValidatedContext(Context, Scheme, Options)
                {
                    Principal = principal
                };

                await certEvents.CertificateValidated(certValidatedContext);

                if (certValidatedContext.Result != null)
                {
                    return certValidatedContext.Result;
                }

                principal = certValidatedContext.Principal ?? principal;
            }

            Host.Logger.Information("Client certificate authentication succeeded for subject: {Subject}", clientCertificate.Subject);

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
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
    /// Helper class to validate X509 certificates.
    /// </summary>
    private class CertificateValidator(ClientCertificateAuthenticationOptions options)
    {
        private readonly ClientCertificateAuthenticationOptions _options = options;

        public Task<(bool Success, string? FailureMessage)> ValidateAsync(X509Certificate2 certificate)
        {
            // Check allowed certificate types
            if (_options.AllowedCertificateTypes != CertificateTypes.All)
            {
                var isSelfSigned = certificate.Subject == certificate.Issuer;
                var isAllowed = _options.AllowedCertificateTypes switch
                {
                    CertificateTypes.Chained => !isSelfSigned,
                    CertificateTypes.SelfSigned => isSelfSigned,
                    _ => true
                };

                if (!isAllowed)
                {
                    return Task.FromResult<(bool Success, string? FailureMessage)>((false, $"Certificate type not allowed: {(isSelfSigned ? "SelfSigned" : "Chained")}"));
                }
            }

            // Check validity period
            if (_options.ValidateValidityPeriod)
            {
                var now = DateTime.UtcNow;
                if (certificate.NotBefore > now || certificate.NotAfter < now)
                {
                    return Task.FromResult<(bool Success, string? FailureMessage)>((false, "Certificate is not within its validity period"));
                }
            }

            // Check certificate use
            if (_options.ValidateCertificateUse)
            {
                var hasClientAuth = false;
                foreach (var extension in certificate.Extensions)
                {
                    if (extension is X509EnhancedKeyUsageExtension eku)
                    {
                        foreach (var oid in eku.EnhancedKeyUsages)
                        {
                            // 1.3.6.1.5.5.7.3.2 is Client Authentication
                            if (oid.Value == "1.3.6.1.5.5.7.3.2")
                            {
                                hasClientAuth = true;
                                break;
                            }
                        }
                    }
                }

                if (!hasClientAuth)
                {
                    return Task.FromResult<(bool Success, string? FailureMessage)>((false, "Certificate does not have Client Authentication usage"));
                }
            }

            // Check revocation if needed
            if (_options.RevocationMode != X509RevocationMode.NoCheck)
            {
                using var chain = new X509Chain
                {
                    ChainPolicy =
                    {
                        RevocationMode = _options.RevocationMode,
                        RevocationFlag = _options.RevocationFlag
                    }
                };

                if (_options.CustomTrustStore != null)
                {
                    chain.ChainPolicy.CustomTrustStore.AddRange(_options.CustomTrustStore);
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                }

                var isValid = chain.Build(certificate);
                if (!isValid)
                {
                    var errors = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation));
                    return Task.FromResult<(bool Success, string? FailureMessage)>((false, $"Certificate chain validation failed: {errors}"));
                }
            }

            return Task.FromResult<(bool Success, string? FailureMessage)>((true, null));
        }
    }
}
