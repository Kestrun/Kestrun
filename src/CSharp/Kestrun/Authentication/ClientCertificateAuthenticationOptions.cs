using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Kestrun.Hosting;

namespace Kestrun.Authentication;

/// <summary>
/// Options for configuring Client Certificate Authentication in Kestrun.
/// </summary>
public class ClientCertificateAuthenticationOptions : CertificateAuthenticationOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions
{
    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;

    /// <inheritdoc/>
    public bool Deprecated { get; set; }

    private Serilog.ILogger? _logger;

    /// <inheritdoc/>
    public Serilog.ILogger Logger
    {
        get => _logger ?? (Host is null ? Serilog.Log.Logger : Host.Logger);
        set => _logger = value;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied ClientCertificateAuthenticationOptions instance 
    /// to the instance created by the framework inside AddCertificate().
    /// </summary>
    /// <param name="target">The target instance to which values will be copied.</param>
    public void ApplyTo(ClientCertificateAuthenticationOptions target)
    {
        // Copy base CertificateAuthenticationOptions properties
        target.AllowedCertificateTypes = AllowedCertificateTypes;
        target.ChainTrustValidationMode = ChainTrustValidationMode;
        target.CustomTrustStore = CustomTrustStore;
        target.RevocationFlag = RevocationFlag;
        target.RevocationMode = RevocationMode;
        target.ValidateCertificateUse = ValidateCertificateUse;
        target.ValidateValidityPeriod = ValidateValidityPeriod;

        // Copy event handlers
        target.Events = Events;

        // Copy IAuthenticationHostOptions properties
        target.Host = Host;

        // OpenAPI / documentation properties (IOpenApiAuthenticationOptions)
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DocumentationId = DocumentationId;
        target.DisplayName = DisplayName;
        target.Deprecated = Deprecated;
    }
}
