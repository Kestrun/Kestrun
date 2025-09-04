<#
    .SYNOPSIS
        Creates a new Kestrun server instance with specified options and listeners.
    .DESCRIPTION
        This function initializes a new Kestrun server instance, allowing configuration of various options and listeners.
    .PARAMETER Server
        The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.
    .PARAMETER Port
        The port on which the server will listen for incoming requests. This parameter is mandatory.
    .PARAMETER IPAddress
        The IP address on which the server will listen. Defaults to [System.Net.IPAddress]::Any, which means it will listen on all available network interfaces.
    .PARAMETER CertPath
        The path to the SSL certificate file. This parameter is mandatory if using HTTPS.
    .PARAMETER CertPassword
        The password for the SSL certificate, if applicable. This parameter is optional.
    .PARAMETER X509Certificate
        An X509Certificate2 object representing the SSL certificate. This parameter is mandatory if using HTTPS
    .PARAMETER Protocols
        The HTTP protocols to use (e.g., Http1, Http2). Defaults to Http1 for HTTP listeners and Http1OrHttp2 for HTTPS listeners.
    .PARAMETER UseConnectionLogging
        If specified, enables connection logging for the listener. This is useful for debugging and monitoring purposes.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after adding the listener.
    .EXAMPLE
        New-KrServer -Name 'MyKestrunServer'
        Creates a new Kestrun server instance with the specified name.
    .NOTES
        This function is designed to be used after the server has been configured with routes and listeners.
#>
function Add-KrListener {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'NoCert')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [System.Net.IPAddress]$IPAddress = [System.Net.IPAddress]::Loopback,
        [Parameter(mandatory = $true, ParameterSetName = 'CertFile')]
        [string]$CertPath,

        [Parameter(mandatory = $false, ParameterSetName = 'CertFile')]
        [SecureString]$CertPassword = $null,

        [Parameter(mandatory = $true, ParameterSetName = 'x509Certificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$X509Certificate = $null,

        [Parameter(ParameterSetName = 'x509Certificate')]
        [Parameter(ParameterSetName = 'CertFile')]
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols,

        [Parameter()]
        [switch]$UseConnectionLogging,

        [Parameter()]
        [switch]$PassThru
    )

    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # Validate parameters based on the parameter set
        if ($null -eq $Protocols) {
            if ($PSCmdlet.ParameterSetName -eq 'NoCert') {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1
            } else {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1OrHttp2
            }
        }
        if ($PSCmdlet.ParameterSetName -eq 'CertFile') {
            if (-not (Test-Path $CertPath)) {
                throw "Certificate file not found: $CertPath"
            }
            $X509Certificate = Import-KestrunCertificate -Path $CertPath -Password $CertPassword
        }


        $Server.ConfigureListener($Port, $IPAddress, $X509Certificate, $Protocols, $UseConnectionLogging.IsPresent) | Out-Null
        if ($PassThru.IsPresent) {
            # Return the modified server instance
            return $Server
        }
    }
}

