function Set-KrServerLimit {
    [KestrunRuntimeApi([KestrunApiContext]::Definition)]
    [CmdletBinding(SupportsShouldProcess = $true)]
    <#
.SYNOPSIS
    Configures advanced options and operational limits for a Kestrun server instance.

.DESCRIPTION
    This function allows administrators to fine-tune the behavior of a Kestrun server by setting various
    operational limits and options.

.PARAMETER Server
    The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.

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

#>
    [KestrunRuntimeApi([KestrunApiContext]::Definition)]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [long]$MaxRequestBodySize , # Default is 30,000,000 
        [int]$MaxConcurrentConnections ,
        [int]$MaxRequestHeaderCount , # Default is 100 
        [int]$KeepAliveTimeoutSeconds  , # Default is 130 seconds
        [long]$MaxRequestBufferSize , #default is 1,048,576 bytes (1 MB).
        [int]$MaxRequestHeadersTotalSize , # Default is 32,768 bytes (32 KB)
        [int]$MaxRequestLineSize , # Default is 8,192 bytes (8 KB)
        [long]$MaxResponseBufferSize  , # Default is  65,536 bytes (64 KB).
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinRequestBodyDataRate , # Defaults to 240 bytes/second with a 5 second grace period.
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinResponseDataRate, # Defaults to 240 bytes/second with a 5 second grace period.
        [int]$RequestHeadersTimeoutSeconds, # Default is 30 seconds.
        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        
        if ($PSCmdlet.ShouldProcess("Kestrun server Limits", "Set server limits")) {
            $options = $Server.Options
            if ($null -eq $options) {
                throw "Server is not initialized. Please ensure the server is configured before setting limits."
            }
            if ($MaxRequestBodySize -gt 0) {
                $options.ServerLimits.MaxRequestBodySize = $MaxRequestBodySize
            }
            if ($MaxConcurrentConnections -gt 0) {
                $options.ServerLimits.MaxConcurrentConnections = $MaxConcurrentConnections
            }
            if ($MaxRequestHeaderCount -gt 0) {
                $options.ServerLimits.MaxRequestHeaderCount = $MaxRequestHeaderCount
            }
            if ($KeepAliveTimeoutSeconds -gt 0) {
                $options.ServerLimits.KeepAliveTimeout = [TimeSpan]::FromSeconds($KeepAliveTimeoutSeconds)
            }
            if ($MaxRequestBufferSize -gt 0) {
                $options.ServerLimits.MaxRequestBufferSize = $MaxRequestBufferSize
            }
            if ($MaxRequestHeadersTotalSize -gt 0) {
                $options.ServerLimits.MaxRequestHeadersTotalSize = $MaxRequestHeadersTotalSize
            }
            if ($MaxRequestLineSize -gt 0) {
                $options.ServerLimits.MaxRequestLineSize = $MaxRequestLineSize
            }
            if ($MaxResponseBufferSize -gt 0) {
                $options.ServerLimits.MaxResponseBufferSize = $MaxResponseBufferSize
            }
            if ($null -ne $MinRequestBodyDataRate) {
                $options.ServerLimits.MinRequestBodyDataRate = $MinRequestBodyDataRate
            }
            if ($null -ne $MinResponseDataRate) {
                $options.ServerLimits.MinResponseDataRate = $MinResponseDataRate
            }
            if ($null -ne $RequestHeadersTimeout) {
                $options.ServerLimits.RequestHeadersTimeout = [TimeSpan]::FromSeconds($RequestHeadersTimeoutSeconds)
            }
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }

    }
}