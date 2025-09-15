<#
.SYNOPSIS
    Configures HTTPS options for a Kestrun server instance.
.DESCRIPTION
    This function allows administrators to set or modify the HTTPS connection adapter options for a Kestrun
server instance, including SSL protocols, client certificate modes, and server certificates.
.PARAMETER Server
    The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.
.PARAMETER Options
    The HttpsConnectionAdapterOptions object containing the desired HTTPS configuration settings.
.PARAMETER SslProtocols
    Specifies the SSL protocols to be used for HTTPS connections. This parameter is optional and can be set to a specific protocol or left unset to use defaults.
.PARAMETER ClientCertificateMode
    Specifies the client certificate mode for HTTPS connections. This parameter is optional and can be set to a specific mode or left unset to use defaults.
.PARAMETER CheckCertificateRevocation
    If specified, enables certificate revocation checking for HTTPS connections. This parameter is optional and can be left unset to use defaults.
.PARAMETER ServerCertificate
    Specifies the server certificate to be used for HTTPS connections. This parameter is optional and can be left unset to use defaults.
.PARAMETER ServerCertificateChain
    Specifies the server certificate chain to be used for HTTPS connections. This parameter is optional and can be left unset to use defaults.
.PARAMETER HandshakeTimeout
    Specifies the handshake timeout duration in seconds for HTTPS connections. This parameter is optional and can be left unset to use defaults.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after applying the HTTPS options.
.OUTPUTS
    [Kestrun.Hosting.KestrunHost]
    The modified Kestrun server instance with the applied HTTPS options.
.EXAMPLE
    Set-KrServerHttpsOptions -Server $server -SslProtocols Tls12
    This command sets the SSL protocols for the specified Kestrun server instance to use TLS 1.2.
.EXAMPLE
    Set-KrServerHttpsOptions -Server $server -ClientCertificateMode RequireCertificate
    This command sets the client certificate mode for the specified Kestrun server instance to require a client certificate.
.EXAMPLE
    Set-KrServerHttpsOptions -Server $server -CheckCertificateRevocation
    This command enables certificate revocation checking for the specified Kestrun server instance.
.EXAMPLE
    Set-KrServerHttpsOptions -Server $server -ServerCertificate $cert
    This command sets the server certificate for the specified Kestrun server instance.
.EXAMPLE
    Set-KrServerHttpsOptions -Server $server -HandshakeTimeout 30
    This command sets the handshake timeout for the specified Kestrun server instance to 30 seconds.
.NOTES
    This function is designed to be used in the context of a Kestrun server setup and allows for flexible configuration of HTTPS options.
    $ClientCertificateValidation, $ServerCertificateSelector, and $OnAuthenticate are currently not implemented in this cmdlet but can be added in future versions for more advanced scenarios.
#>
function Set-KrServerHttpsOptions {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Server.Kestrel.Https.HttpsConnectionAdapterOptions]$Options,
        [Parameter( ParameterSetName = 'Items')]
        [System.Security.Authentication.SslProtocols]$SslProtocols,
        [Parameter( ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode]$ClientCertificateMode,
        [Parameter( ParameterSetName = 'Items')]
        [switch]$CheckCertificateRevocation,
        [Parameter( ParameterSetName = 'Items')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$ServerCertificate,
        [Parameter( ParameterSetName = 'Items')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]$ServerCertificateChain,
        [Parameter( ParameterSetName = 'Items')]
        [int]$HandshakeTimeout,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {

        if ($PSCmdlet.ParameterSetName -eq 'Items') {

            $Options = [Microsoft.AspNetCore.Server.Kestrel.Https.HttpsConnectionAdapterOptions]::new()
            if ($null -ne $SslProtocols) {
                $options.SslProtocols = $SslProtocols
            }
            if ($null -ne $ClientCertificateMode) {
                $options.ClientCertificateMode = $ClientCertificateMode
            }
            if ($CheckCertificateRevocation.IsPresent) {
                $Options.CheckCertificateRevocation = $true
            }
            if ($null -ne $ServerCertificate) {
                $Options.ServerCertificate = $ServerCertificate
            }
            if ($null -ne $ServerCertificateChain) {
                $Options.ServerCertificateChain = $ServerCertificateChain
            }
            if ($null -ne $HandshakeTimeout) {
                $Options.HandshakeTimeout = [System.TimeSpan]::FromSeconds($HandshakeTimeout)
            }
        }

        $Server.Options.HttpsConnectionAdapter = $Options

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

