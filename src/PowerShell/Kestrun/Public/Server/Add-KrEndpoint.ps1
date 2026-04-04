<#
    .SYNOPSIS
        Adds a Kestrun endpoint using explicit parameters or environment-based binding.
    .DESCRIPTION
        Adds an HTTP or HTTPS endpoint to the current Kestrun server definition.
        The listener target can be supplied explicitly with -Uri, -HostName, -Port,
        and/or -IPAddress, or resolved from environment variables when no explicit
        binding target was provided.

        Binding precedence:

        1. Explicit -Uri
        2. Explicit -HostName
        3. Explicit -Port / -IPAddress
        4. ASPNETCORE_URLS environment variable
        5. PORT environment variable
        6. Built-in defaults

        ASPNETCORE_URLS supports ASP.NET Core style values such as
        'http://localhost:5000', 'http://+:8080', or a semicolon-delimited list,
        where the first non-empty entry is used. When ASPNETCORE_URLS is not set,
        PORT can be used to bind to 0.0.0.0 on the specified port.
    .PARAMETER Port
        The port on which the server will listen for incoming requests. When no
        explicit binding target is provided, this value may be resolved from the
        PORT environment variable instead.
    .PARAMETER IPAddress
        The IP address on which the server will listen. If omitted and no other
        explicit binding target is supplied, Add-KrEndpoint may resolve the listener
        from ASPNETCORE_URLS or PORT before falling back to the default binding.
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
        If specified, enables connection logging for the listener. This is useful for debugging and monitoring purposes. This parameter is optional.
    .PARAMETER PassThru
        If specified, returns one or more endpoint spec strings that can be passed
        directly to Add-KrMapRoute -Endpoints for listener-specific routing.
    .EXAMPLE
        New-KrServer -Name 'MyKestrunServer'
        Add-KrEndpoint -Port 5000 -IPAddress ([System.Net.IPAddress]::Loopback)
        Adds an explicit loopback listener on port 5000.
    .EXAMPLE
        $env:PORT = '8080'
        New-KrServer -Name 'MyKestrunServer'
        Add-KrEndpoint
        Uses the PORT environment variable and binds to 0.0.0.0:8080.
    .EXAMPLE
        $env:ASPNETCORE_URLS = 'http://localhost:5000;http://127.0.0.1:5001'
        New-KrServer -Name 'MyKestrunServer'
        Add-KrEndpoint
        Uses the first ASPNETCORE_URLS entry and binds to localhost:5000.
    .EXAMPLE
        $httpsEndpoint = Add-KrEndpoint -Port 5443 -CertPath .\devcert.pfx -CertPassword $pw -PassThru
        Add-KrMapRoute -Pattern '/secure' -Endpoints $httpsEndpoint -ScriptBlock { Write-KrTextResponse 'Secure hello' }
        Adds an HTTPS listener and returns route endpoint specs for endpoint-specific routing.
    .NOTES
        This function is designed to be used while staging server listeners before
        Enable-KrConfiguration is called.
#>
function Add-KrEndpoint {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'NoCert')]
    [OutputType([string[]])]
    param(
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
        [alias('SelfSigned')]
        [alias('SelfSignedCertificate')]
        [switch]$SelfSignedCert,

        [Parameter(Mandatory = $true, ParameterSetName = 'x509Certificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$X509Certificate,

        [Parameter(ParameterSetName = 'x509Certificate')]
        [Parameter(ParameterSetName = 'CertFile')]
        [Parameter(ParameterSetName = 'SelfSignedCert')]
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols,

        [Parameter()]
        [switch]$UseConnectionLogging,

        [Parameter()]
        [switch]$PassThru
    )
    # Ensure the server instance is resolved
    $Server = Resolve-KestrunServer

    # Prevent adding endpoints to a server that is already configured
    if ($Server.IsConfigured) {
        throw 'Cannot add endpoint to a server that is already configured. Please create a new server instance.'
    }

    # Validate mutually exclusive parameters
    if ($null -ne $IPAddress) {
        if (-not [string]::IsNullOrEmpty($HostName)) {
            throw 'Cannot specify both IPAddress and HostName. Please choose one.'
        }
        if ($null -ne $Uri) {
            throw 'Cannot specify both IPAddress and Uri. Please choose one.'
        }
        if ($AddressFamily -and -not ($AddressFamily -contains $IPAddress.AddressFamily)) {
            throw 'The specified IPAddress does not match the provided AddressFamily filter.'
        }
    } else {
        if ($null -ne $Uri -and (-not ([string]::IsNullOrEmpty($HostName)))) {
            throw 'Cannot specify both HostName and Uri. Please choose one.'
        }
    }

    # Validate parameters based on the parameter set
    if ($null -eq $Protocols) {
        if ($PSCmdlet.ParameterSetName -eq 'NoCert') {
            $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1
        } else {
            $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2
        }
    }

    # Handle certificate loading based on the parameter set
    if ($PSCmdlet.ParameterSetName -eq 'CertFile') {
        if (-not (Test-Path $CertPath)) {
            throw "Certificate file not found: $CertPath"
        }
        $X509Certificate = Import-KrCertificate -FilePath $CertPath -Password $CertPassword
    } elseif ($SelfSignedCert.IsPresent) {
        $X509Certificate = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -ValidDays 30
    }

    $defaultIPAddress = [System.Net.IPAddress]::Loopback
    if ($null -eq $IPAddress -and $AddressFamily) {
        $requestedFamilies = [System.Net.Sockets.AddressFamily[]]($AddressFamily | Select-Object -Unique)
        $ipv4Requested = $requestedFamilies -contains [System.Net.Sockets.AddressFamily]::InterNetwork
        $ipv6Requested = $requestedFamilies -contains [System.Net.Sockets.AddressFamily]::InterNetworkV6

        if ($ipv6Requested -and -not $ipv4Requested) {
            $defaultIPAddress = [System.Net.IPAddress]::IPv6Loopback
        }
    }

    # Resolve the binding information based on the provided parameters and environment variables
    $binding = Resolve-KrEndpointBinding `
        -BoundParameters $PSBoundParameters `
        -Port $Port `
        -IPAddress $IPAddress `
        -HostName $HostName `
        -Uri $Uri `
        -DefaultPort 5000 `
        -DefaultIPAddress $defaultIPAddress

    switch ($binding.Mode) {
        'Uri' {
            $Server.ConfigureListener(
                $binding.Uri,
                $X509Certificate,
                $Protocols,
                $UseConnectionLogging.IsPresent,
                $AddressFamily
            ) | Out-Null
        }

        'HostName' {
            $Server.ConfigureListener(
                $binding.HostName,
                $binding.Port,
                $X509Certificate,
                $Protocols,
                $UseConnectionLogging.IsPresent,
                $AddressFamily
            ) | Out-Null
        }

        'PortIPAddress' {
            $Server.ConfigureListener(
                $binding.Port,
                $binding.IPAddress,
                $X509Certificate,
                $Protocols,
                $UseConnectionLogging.IsPresent
            ) | Out-Null
        }

        default {
            throw "Unsupported binding mode '$($binding.Mode)'."
        }
    }

    if ($PassThru.IsPresent) {
        return $binding.EndpointNames
    }
}
