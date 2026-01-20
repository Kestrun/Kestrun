<#
    .SYNOPSIS
        Adds Client Certificate authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use client certificate authentication for incoming requests.
        This allows the server to authenticate users based on their X.509 client certificates.
    .PARAMETER Server
        The Kestrun server instance to configure.
        If not specified, the current server instance is used.
    .PARAMETER AuthenticationScheme
        The name of the client certificate authentication scheme (default is 'Certificate').
    .PARAMETER DisplayName
        The display name for the authentication scheme.
    .PARAMETER Description
        A description of the client certificate authentication scheme.
    .PARAMETER Deprecated
        If specified, marks the authentication scheme as deprecated in OpenAPI documentation.
    .PARAMETER DocId
        The documentation IDs to associate with this authentication scheme.
    .PARAMETER Options
        The Client Certificate authentication options to configure.
        If not specified, default options are used.
    .PARAMETER AllowedCertificateTypes
        Specifies which certificate types are allowed (Chained, SelfSigned, or All).
    .PARAMETER ValidateCertificateUse
        If specified, validates that the certificate is valid for client authentication.
    .PARAMETER ValidateValidityPeriod
        If specified, validates that the certificate is within its validity period.
    .PARAMETER RevocationMode
        The revocation mode to use when validating certificates (NoCheck, Online, Offline).
    .PARAMETER Logger
        A logger to use for logging authentication events.
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the authentication.
    .EXAMPLE
        Add-KrClientCertificateAuthentication -Server $server -PassThru
        This example adds client certificate authentication to the specified Kestrun server instance and returns the modified instance.
    .EXAMPLE
        Add-KrClientCertificateAuthentication -Server $server -AllowedCertificateTypes Chained -ValidateCertificateUse -PassThru
        This example adds client certificate authentication with strict validation to the Kestrun server.
    .LINK
        https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth
    .NOTES
        This cmdlet is used to configure client certificate authentication for the Kestrun server,
        allowing you to secure your APIs with X.509 certificates.
        Maps to Kestrun.Hosting.KestrunHostAuthnExtensions.AddClientCertificateAuthentication
#>
function Add-KrClientCertificateAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'v1')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::CertificateSchemeName,

        [Parameter()]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::CertificateDisplayName,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [switch]$Deprecated,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.ClientCertificateAuthenticationOptions]$Options,

        [Parameter(ParameterSetName = 'v1')]
        [Microsoft.AspNetCore.Authentication.Certificate.CertificateTypes]$AllowedCertificateTypes,

        [Parameter(ParameterSetName = 'v1')]
        [switch]$ValidateCertificateUse,

        [Parameter(ParameterSetName = 'v1')]
        [switch]$ValidateValidityPeriod,

        [Parameter(ParameterSetName = 'v1')]
        [System.Security.Cryptography.X509Certificates.X509RevocationMode]$RevocationMode,

        [Parameter(ParameterSetName = 'v1')]
        [Serilog.ILogger]$Logger,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.ClientCertificateAuthenticationOptions]::new()

            # Configure certificate validation options
            if ($PSBoundParameters.ContainsKey('AllowedCertificateTypes')) {
                $Options.AllowedCertificateTypes = $AllowedCertificateTypes
            }
            if ($ValidateCertificateUse.IsPresent) {
                $Options.ValidateCertificateUse = $true
            }
            if ($ValidateValidityPeriod.IsPresent) {
                $Options.ValidateValidityPeriod = $true
            }
            if ($PSBoundParameters.ContainsKey('RevocationMode')) {
                $Options.RevocationMode = $RevocationMode
            }

            # Configure description and deprecated flag
            if (-not [string]::IsNullOrWhiteSpace($Description)) {
                $Options.Description = $Description
            }
            $Options.Deprecated = $Deprecated.IsPresent

            # Configure logger
            if ($null -ne $Logger) {
                $Options.Logger = $Logger
            }

            # Set OpenApi documentation IDs
            $Options.DocumentationId = $DocId
        }

        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddClientCertificateAuthentication(
            $Server,
            $AuthenticationScheme,
            $DisplayName,
            $Options
        ) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
