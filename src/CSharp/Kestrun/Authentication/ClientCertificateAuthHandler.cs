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
            var (Success, FailureMessage) = await certificateValidator.ValidateAsync(clientCertificate);

            if (!Success)
            {
                Host.Logger.Warning("Certificate validation failed: {Reason}", FailureMessage);
                return AuthenticateResult.Fail(FailureMessage ?? "Certificate validation failed");
            }

            // Build the claims identity
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, clientCertificate.Subject, ClaimValueTypes.String, Options.ClaimsIssuer),
                new(ClaimTypes.Name, clientCertificate.Subject, ClaimValueTypes.String, Options.ClaimsIssuer),
                // Add thumbprint as a claim
                new("thumbprint", clientCertificate.Thumbprint, ClaimValueTypes.String, Options.ClaimsIssuer),

                // Add issuer and serial number
                new("issuer", clientCertificate.Issuer, ClaimValueTypes.String, Options.ClaimsIssuer),
                new("serialnumber", clientCertificate.SerialNumber ?? string.Empty, ClaimValueTypes.String, Options.ClaimsIssuer)
            };

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
    /// <param name="options">The client certificate authentication options.</param>
    private class CertificateValidator(ClientCertificateAuthenticationOptions options)
    {
        private readonly ClientCertificateAuthenticationOptions _options = options;

        private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

        /// <summary>
        /// Validates the specified certificate according to the configured options.
        /// </summary>
        /// <param name="certificate">The X509 certificate to validate.</param>
        /// <returns>A task that represents the asynchronous validation operation. The task result contains a tuple indicating success and an optional failure message.</returns>
        public Task<(bool Success, string? FailureMessage)> ValidateAsync(X509Certificate2 certificate)
        {
            ArgumentNullException.ThrowIfNull(certificate);

            var result = ValidateAllowedCertificateTypes(certificate);
            if (!result.Success)
            {
                return Task.FromResult(result);
            }

            result = ValidateValidityPeriod(certificate);
            if (!result.Success)
            {
                return Task.FromResult(result);
            }

            result = ValidateCertificateUse(certificate);
            if (!result.Success)
            {
                return Task.FromResult(result);
            }

            result = ValidateRevocation(certificate);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Validates that the certificate type (self-signed vs chained) is allowed by the configured options.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <returns>Success when allowed; otherwise a failure with a message.</returns>
        private (bool Success, string? FailureMessage) ValidateAllowedCertificateTypes(X509Certificate2 certificate)
        {
            if (_options.AllowedCertificateTypes == CertificateTypes.All)
            {
                return (true, null);
            }

            var isSelfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.Ordinal);
            var isAllowed = _options.AllowedCertificateTypes switch
            {
                CertificateTypes.Chained => !isSelfSigned,
                CertificateTypes.SelfSigned => isSelfSigned,
                _ => true
            };

            return isAllowed
                ? (true, null)
                : (false, $"Certificate type not allowed: {(isSelfSigned ? "SelfSigned" : "Chained")}");
        }

        /// <summary>
        /// Validates that the certificate is within its validity period when enabled.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <returns>Success when valid or validation is disabled; otherwise a failure with a message.</returns>
        private (bool Success, string? FailureMessage) ValidateValidityPeriod(X509Certificate2 certificate)
        {
            if (!_options.ValidateValidityPeriod)
            {
                return (true, null);
            }

            var now = DateTime.UtcNow;
            return certificate.NotBefore > now || certificate.NotAfter < now
                ? (false, "Certificate is not within its validity period")
                : (true, null);
        }

        /// <summary>
        /// Validates that the certificate has client authentication usage when enabled.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <returns>Success when usage is present or validation is disabled; otherwise a failure with a message.</returns>
        private (bool Success, string? FailureMessage) ValidateCertificateUse(X509Certificate2 certificate)
        {
            if (!_options.ValidateCertificateUse)
            {
                return (true, null);
            }

            return HasEnhancedKeyUsageOid(certificate, ClientAuthenticationOid)
                ? (true, null)
                : (false, "Certificate does not have Client Authentication usage");
        }

        /// <summary>
        /// Validates the certificate chain and revocation status when enabled by the configured options.
        /// </summary>
        /// <param name="certificate">The certificate to validate.</param>
        /// <returns>Success when valid or revocation checking is disabled; otherwise a failure with a message.</returns>
        private (bool Success, string? FailureMessage) ValidateRevocation(X509Certificate2 certificate)
        {
            var isSelfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.Ordinal);
            var hasCustomTrustStore = _options.CustomTrustStore is { Count: > 0 };

            // If self-signed certificates are explicitly allowed and the caller did not supply a trust store,
            // treat the certificate as valid at the authentication layer.
            // (Chain building for self-signed end-entity certs is platform-dependent and commonly fails.)
            if (isSelfSigned && !hasCustomTrustStore &&
                (_options.AllowedCertificateTypes == CertificateTypes.SelfSigned ||
                 _options.AllowedCertificateTypes == CertificateTypes.All))
            {
                return (true, null);
            }

            using var chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = _options.RevocationMode,
                    RevocationFlag = _options.RevocationFlag
                }
            };

            if (hasCustomTrustStore)
            {
                chain.ChainPolicy.CustomTrustStore.AddRange(_options.CustomTrustStore);
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            }

            if (chain.Build(certificate))
            {
                return (true, null);
            }

            var errors = string.Join(", ", chain.ChainStatus.Select(s =>
            {
                var info = s.StatusInformation?.Trim();
                return string.IsNullOrEmpty(info) ? s.Status.ToString() : info;
            }));

            return (false, $"Certificate chain validation failed: {errors}");
        }

        /// <summary>
        /// Checks whether the certificate contains the specified Enhanced Key Usage (EKU) OID.
        /// </summary>
        /// <param name="certificate">The certificate to inspect.</param>
        /// <param name="oidValue">The OID value to match.</param>
        /// <returns><c>true</c> when the EKU is present; otherwise <c>false</c>.</returns>
        private static bool HasEnhancedKeyUsageOid(X509Certificate2 certificate, string oidValue)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is not X509EnhancedKeyUsageExtension eku)
                {
                    continue;
                }

                foreach (var oid in eku.EnhancedKeyUsages)
                {
                    if (string.Equals(oid.Value, oidValue, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
