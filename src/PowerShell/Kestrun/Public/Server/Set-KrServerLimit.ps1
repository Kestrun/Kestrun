<#
    .SYNOPSIS
        Configures advanced options and operational limits for a Kestrun server instance.
    .DESCRIPTION
        This function allows administrators to fine-tune the behavior of a Kestrun server by setting various
        operational limits and options.
    .PARAMETER Server
        The Kestrun server instance to configure.This parameter is mandatory and must be a valid server object.
    .PARAMETER MaxRequestBodySize
        Specifies the maximum allowed size of the HTTP request body in bytes.
        Requests exceeding this size will be rejected.
        Default: 30,000,000 bytes (28.6 MB).
    .PARAMETER MaxConcurrentConnections
        Sets the maximum number of concurrent client connections allowed to the server.
        Additional connection attempts will be queued or rejected.
        Default: Unlimited (no explicit limit).
    .PARAMETER MaxRequestHeaderCount
        Defines the maximum number of HTTP headers permitted in a single request.
        Requests with more headers will be rejected.
        Default: 100.
    .PARAMETER KeepAliveTimeoutSeconds
        Specifies the duration, in seconds, that a connection is kept alive when idle before being closed.
        Default: 120 seconds.
    .PARAMETER MaxRequestBufferSize
        Sets the maximum size, in bytes, of the buffer used for reading HTTP requests.
        Default: 1048576 bytes (1 MB).
    .PARAMETER MaxRequestHeadersTotalSize
        Specifies the maximum combined size, in bytes, of all HTTP request headers.
        Requests exceeding this size will be rejected.
        Default: 32768 bytes (32 KB).
    .PARAMETER MaxRequestLineSize
        Sets the maximum allowed length, in bytes, of the HTTP request line (method, URI, and version).
        Default: 8192 bytes (8 KB).
    .PARAMETER MaxResponseBufferSize
        Specifies the maximum size, in bytes, of the buffer used for sending HTTP responses.
        Default: 65536 bytes (64 KB).
    .PARAMETER MinRequestBodyDataRate
        Defines the minimum data rate, in bytes per second, required for receiving the request body.
        If the rate falls below this threshold, the connection may be closed.
        Default: 240 bytes/second.
    .PARAMETER MinResponseDataRate
        Sets the minimum data rate, in bytes per second, required for sending the response.
        Default: 240 bytes/second.
    .PARAMETER RequestHeadersTimeoutSeconds
        Specifies the maximum time, in seconds, allowed to receive the complete set of request headers.
        Default: 30 seconds.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after applying the limits.
    .OUTPUTS
        [Kestrun.Hosting.KestrunHost]
        The modified Kestrun server instance after applying the limits.
    .EXAMPLE
        Set-KrServerLimit -Server $server -MaxRequestBodySize 30000000
        Applies the specified limits to the Kestrun server instance.
    .EXAMPLE
        Set-KrServerLimit -Server $server -MinRequestBodyDataRate 240
        Sets the minimum data rate for receiving the request body.
    .EXAMPLE
        Set-KrServerLimit -Server $server -MaxResponseBufferSize 65536
        Sets the maximum size of the buffer used for sending HTTP responses.
    .EXAMPLE
        Set-KrServerLimit -Server $server -MinResponseDataRate 240
        Sets the minimum data rate for sending the response.
    .EXAMPLE
        Set-KrServerLimit -Server $server -MaxRequestBodySize 30000000
        Applies the specified limits to the Kestrun server instance.
    .NOTES
        This cmdlet modifies the server instance's configuration to enforce the specified limits.
#>
function Set-KrServerLimit {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [long]$MaxRequestBodySize , # Default is 30,000,000
        [Parameter()]
        [int]$MaxConcurrentConnections ,
        [Parameter()]
        [int]$MaxRequestHeaderCount , # Default is 100
        [Parameter()]
        [int]$KeepAliveTimeoutSeconds  , # Default is 130 seconds
        [Parameter()]
        [long]$MaxRequestBufferSize , #default is 1,048,576 bytes (1 MB).
        [Parameter()]
        [int]$MaxRequestHeadersTotalSize , # Default is 32,768 bytes (32 KB)
        [Parameter()]
        [int]$MaxRequestLineSize , # Default is 8,192 bytes (8 KB)
        [Parameter()]
        [long]$MaxResponseBufferSize  , # Default is  65,536 bytes (64 KB).
        [Parameter()]
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinRequestBodyDataRate , # Defaults to 240 bytes/second with a 5 second grace period.
        [Parameter()]
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinResponseDataRate, # Defaults to 240 bytes/second with a 5 second grace period.
        [Parameter()]
        [int]$RequestHeadersTimeoutSeconds, # Default is 30 seconds.
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $options = $Server.Options
        if ($null -eq $options) {
            throw 'Server is not initialized.Please ensure the server is configured before setting limits.'
        }
        if ($MaxRequestBodySize -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxRequestBodySize to {MaxRequestBodySize} bytes" -Values $MaxRequestBodySize
            $options.ServerLimits.MaxRequestBodySize = $MaxRequestBodySize
        }
        if ($MaxConcurrentConnections -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxConcurrentConnections to {MaxConcurrentConnections}" -Values $MaxConcurrentConnections
            $options.ServerLimits.MaxConcurrentConnections = $MaxConcurrentConnections
        }
        if ($MaxRequestHeaderCount -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxRequestHeaderCount to {MaxRequestHeaderCount}" -Values $MaxRequestHeaderCount
            $options.ServerLimits.MaxRequestHeaderCount = $MaxRequestHeaderCount
        }
        if ($KeepAliveTimeoutSeconds -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting KeepAliveTimeout to {KeepAliveTimeoutSeconds} seconds" -Values $KeepAliveTimeoutSeconds
            $options.ServerLimits.KeepAliveTimeout = [TimeSpan]::FromSeconds($KeepAliveTimeoutSeconds)
        }
        if ($MaxRequestBufferSize -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxRequestBufferSize to {MaxRequestBufferSize} bytes" -Values $MaxRequestBufferSize
            $options.ServerLimits.MaxRequestBufferSize = $MaxRequestBufferSize
        }
        if ($MaxRequestHeadersTotalSize -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxRequestHeadersTotalSize to {MaxRequestHeadersTotalSize} bytes" -Values $MaxRequestHeadersTotalSize
            $options.ServerLimits.MaxRequestHeadersTotalSize = $MaxRequestHeadersTotalSize
        }
        if ($MaxRequestLineSize -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxRequestLineSize to {MaxRequestLineSize} bytes" -Values $MaxRequestLineSize
            $options.ServerLimits.MaxRequestLineSize = $MaxRequestLineSize
        }
        if ($MaxResponseBufferSize -gt 0) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MaxResponseBufferSize to {MaxResponseBufferSize} bytes" -Values $MaxResponseBufferSize
            $options.ServerLimits.MaxResponseBufferSize = $MaxResponseBufferSize
        }
        if ($null -ne $MinRequestBodyDataRate) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MinRequestBodyDataRate to {MinRequestBodyDataRate} bytes/second" -Values $MinRequestBodyDataRate
            $options.ServerLimits.MinRequestBodyDataRate = $MinRequestBodyDataRate
        }
        if ($null -ne $MinResponseDataRate) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting MinResponseDataRate to {MinResponseDataRate} bytes/second" -Values $MinResponseDataRate
            $options.ServerLimits.MinResponseDataRate = $MinResponseDataRate
        }
        if ($null -ne $RequestHeadersTimeout) {
            Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting RequestHeadersTimeout to {RequestHeadersTimeout} seconds" -Values $RequestHeadersTimeout
            $options.ServerLimits.RequestHeadersTimeout = [TimeSpan]::FromSeconds($RequestHeadersTimeoutSeconds)
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

