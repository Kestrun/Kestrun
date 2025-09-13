p<#
    .SYNOPSIS
        Creates a new Kestrun server instance with specified options and listeners.
    .DESCRIPTION
        This function initializes a new Kestrun server instance, allowing configuration of various options and listeners.
    .PARAMETER Server
        The Kestrun server instance to configure. This parameter is Mandatory and must be a valid server object.
    .PARAMETER Port
        The port on which the server will listen for incoming requests. The default is 0, which means a random available port will be assigned.
    .PARAMETER IPAddress
        The IP address on which the server will listen. Defaults to [System.Net.IPAddress]::Any, which means it will listen on all available network interfaces.
    .PARAMETER HostName
        The hostname for the listener. This parameter is Mandatory if using the 'HostName' parameter set.
    .PARAMETER Uri
        The full URI for the listener. This parameter is Mandatory if using the 'Uri' parameter set.
    .PARAMETER AddressFamily
        An array of address families to filter resolved addresses (e.g., IPv4-only). This parameter is optional.
    .PARAMETER CertPath
        The path to the SSL certificate file. This parameter is Mandatory if using HTTPS.
    .PARAMETER CertPassword
        The password for the SSL certificate, if applicable. This parameter is optional.
    .PARAMETER SelfSignedCert
        If specified, a self-signed certificate will be generated and used for HTTPS. This parameter is optional.
    .PARAMETER X509Certificate
        An X509Certificate2 object representing the SSL certificate. This parameter is Mandatory if using HTTPS
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
        [Parameter()]
        [int]$Port = 0,
        [Parameter()]
        [System.Net.IPAddress]$IPAddress,
        [Parameter()]
        [string]$HostName,
        [Parameter()]
        [System.Uri]$Uri,
        [Parameter()]
        [System.Net.Sockets.AddressFamily[]]$AddressFamily,

        [Parameter(mandatory = $true, ParameterSetName = 'CertFile')]
        [string]$CertPath,

        [Parameter(mandatory = $false, ParameterSetName = 'CertFile')]
        [SecureString]$CertPassword = $null,

        [Parameter(ParameterSetName = 'SelfSignedCert')]
        [switch]$SelfSignedCert,

        [Parameter(Mandatory = $true, ParameterSetName = 'x509Certificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$X509Certificate = $null,

        [Parameter(ParameterSetName = 'x509Certificate')]
        [Parameter(ParameterSetName = 'CertFile')]
        [Parameter(ParameterSetName = 'SelfSignedCert')]
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols,

        [Parameter()]
        [switch]$UseConnectionLogging,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
        if ($null -ne $IPAddress) {
            if (-not [string]::IsNullOrEmpty($HostName)) {
                throw "Cannot specify both IPAddress and HostName. Please choose one."
            }
            if ($null -ne $Uri) {
                throw "Cannot specify both IPAddress and Uri. Please choose one."
            }
            if ($AddressFamily -and -not ($AddressFamily -contains $IPAddress.AddressFamily)) {
                throw "The specified IPAddress does not match the provided AddressFamily filter."
            }
        } else {

            if ($null -ne $Uri -and (-not ([string]::IsNullOrEmpty($HostName)))) {
                throw "Cannot specify both HostName and Uri. Please choose one."
            }
            if ($null -eq $Uri -and [string]::IsNullOrEmpty($HostName)) {
                $IPAddress = [System.Net.IPAddress]::Loopback
            }
        }
    }
    process {

        # Validate parameters based on the parameter set
        if ($null -eq $Protocols) {
            if ($PSCmdlet.ParameterSetName -eq 'NoCert') {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1
            } else {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2
            }
        }
        if ($PSCmdlet.ParameterSetName -eq 'CertFile') {
            if (-not (Test-Path $CertPath)) {
                throw "Certificate file not found: $CertPath"
            }
            $X509Certificate = Import-KrCertificate -FilePath $CertPath -Password $CertPassword
        } elseif ($SelfSignedCert.IsPresent) {
            $X509Certificate = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -ValidDays 30
        }


        if (-not ([string]::IsNullOrEmpty($HostName))) {
            $Server.ConfigureListener($HostName, $Port, $X509Certificate, $Protocols, $UseConnectionLogging.IsPresent, $AddressFamily) | Out-Null
        } elseif ($null -ne $Uri) {
            $Server.ConfigureListener($Uri, $X509Certificate, $Protocols, $UseConnectionLogging.IsPresent, $AddressFamily) | Out-Null
        } elseif ($null -ne $IPAddress) {
            $Server.ConfigureListener($Port, $IPAddress, $X509Certificate, $Protocols, $UseConnectionLogging.IsPresent) | Out-Null
        } else {
            throw "Invalid parameter set: $($PSCmdlet.ParameterSetName). Please specify either HostName, Uri, or IPAddress."
        }

        if ($PassThru.IsPresent) {
            # Return the modified server instance
            return $Server
        }
    }
}

