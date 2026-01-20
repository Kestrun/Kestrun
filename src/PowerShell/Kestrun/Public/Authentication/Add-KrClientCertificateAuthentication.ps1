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
    .PARAMETER IssueClaimsScriptBlock
        A script block that contains the logic for issuing claims from the certificate.
    .PARAMETER IssueClaimsCode
        C# or VBNet code that contains the logic for issuing claims from the certificate.
    .PARAMETER IssueClaimsCodeLanguage
        The scripting language of the code used for issuing claims.
    .PARAMETER IssueClaimsCodeFilePath
        Path to a file containing the code that contains the logic for issuing claims from the certificate.
    .PARAMETER ClaimPolicyConfig
        Configuration for claim policies to apply during authentication.
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
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Microsoft.AspNetCore.Authentication.Certificate.CertificateTypes]$AllowedCertificateTypes,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [switch]$ValidateCertificateUse,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [switch]$ValidateValidityPeriod,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [System.Security.Cryptography.X509Certificates.X509RevocationMode]$RevocationMode,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Serilog.ILogger]$Logger,

        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicyConfig,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i1')]
        [scriptblock]$IssueClaimsScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i2')]
        [string]$IssueClaimsCode,

        [Parameter(ParameterSetName = 'v1_i2')]
        [Kestrun.Scripting.ScriptLanguage]$IssueClaimsCodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i3')]
        [string]$IssueClaimsCodeFilePath,

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

            # Configure claim policy
            if ($null -ne $ClaimPolicyConfig) {
                $Options.ClaimPolicyConfig = $ClaimPolicyConfig
            }

            # Configure claims issuer if provided
            if ($PSCmdlet.ParameterSetName.contains('_')) {
                $Options.IssueClaimsCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()

                if ($null -ne $IssueClaimsScriptBlock) {
                    $Options.IssueClaimsCodeSettings.Code = $IssueClaimsScriptBlock.ToString()
                    $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                } elseif (-not [string]::IsNullOrWhiteSpace($IssueClaimsCode)) {
                    $Options.IssueClaimsCodeSettings.Code = $IssueClaimsCode
                    $Options.IssueClaimsCodeSettings.Language = $IssueClaimsCodeLanguage
                } elseif (-not [string]::IsNullOrWhiteSpace($IssueClaimsCodeFilePath)) {
                    if (-not (Test-Path -Path $IssueClaimsCodeFilePath)) {
                        throw "The specified code file path does not exist: $IssueClaimsCodeFilePath"
                    }
                    $extension = Split-Path -Path $IssueClaimsCodeFilePath -Extension
                    switch ($extension) {
                        '.ps1' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                        }
                        '.cs' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                        }
                        '.vb' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                        }
                        default {
                            throw "Unsupported '$extension' code file extension."
                        }
                    }
                    $Options.IssueClaimsCodeSettings.Code = Get-Content -Path $IssueClaimsCodeFilePath -Raw
                }
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
