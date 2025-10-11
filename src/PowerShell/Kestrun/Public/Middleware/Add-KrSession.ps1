<#
    .SYNOPSIS
        Adds session state services and middleware to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use session state for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure. If not specified, the current server instance is used.
    .PARAMETER Options
        The session options to configure. If not specified, default options are used.
    .PARAMETER Cookie
        The cookie configuration to use. If not specified, default cookie settings are applied.
        Can be created with New-KrCookieBuilder and passed via pipeline.
    .PARAMETER IdleTimeout
        The idle timeout in minutes after which the session will expire. If not specified, the default is 20 minutes.
    .PARAMETER IOTimeout
        The IO timeout in seconds for session operations. If not specified, the default is 10 seconds.
    .PARAMETER NoDistributedMemoryCache
        If specified, the cmdlet will not add a default in-memory distributed cache. This is useful if you plan to add your own distributed cache implementation.
    .PARAMETER MemoryCacheOptions
        The configuration options for the in-memory distributed cache. If not specified, default options are used.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified server instance after configuration.
    .EXAMPLE
        Add-KrSession -Server $myServer -Options $mySessionOptions
        Adds session state services and middleware to the specified Kestrun server with the provided options.
    .EXAMPLE
        Add-KrSession -IdleTimeout 30 -IOTimeout 15
        Configures session state with a 30-minute idle timeout and a 15-second IO timeout.
    .EXAMPLE
        $cookie = New-KrCookieBuilder -Name 'SessionCookie' -HttpOnly -SameSite Lax
        Add-KrSession -Cookie $cookie -IdleTimeout 25
        Configures session state with a 25-minute idle timeout and a cookie named 'SessionCookie'.
    .EXAMPLE
        New-KrCookieBuilder -Name 'SessionCookie' -HttpOnly -SameSite Lax |
            Add-KrSession -IdleTimeout 25
        Configures session state with a 25-minute idle timeout and a cookie named 'SessionCookie' created via pipeline.
    .EXAMPLE
        Add-KrSession -NoDistributedMemoryCache
        Configures session state without adding a default in-memory distributed cache. Useful if you plan to add your own distributed cache implementation.
    .EXAMPLE
        $MemoryCacheOptions= [Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions]::new()
        $MemoryCacheOptions.SizeLimit = 1024
        $MemoryCacheOptions.ExpirationScanFrequency = [TimeSpan]::FromMinutes(5)
        Add-KrSession -MemoryCacheOptions $MemoryCacheOptions
        Configures session state and adds a distributed memory cache with a size limit of 1024 bytes and an expiration scan frequency of 5 minutes.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module and is used to configure session state for Kestrun servers.
    .LINK
        https://docs.kestrun.dev/docs/powershell/kestrun/middleware
 #>
function Add-KrSession {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.SessionOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Http.CookieBuilder]$Cookie,

        [Parameter(ParameterSetName = 'Items')]
        [int]$IdleTimeout ,

        [Parameter(ParameterSetName = 'Items')]
        [int]$IOTimeout,

        [Parameter()]
        [switch]$NoDistributedMemoryCache,

        [Parameter()]
        [Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions]$MemoryCacheOptions,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.SessionOptions]::new()

            if ($PsBoundParameters.ContainsKey('IdleTimeout')) {
                $Options.IdleTimeout = [TimeSpan]::FromMinutes($IdleTimeout)
            }
            if ($PsBoundParameters.ContainsKey('IOTimeout')) {
                $Options.IOTimeout = [TimeSpan]::FromSeconds($IOTimeout)
            }
            if ($PsBoundParameters.ContainsKey('Cookie')) {
                $Options.Cookie = $Cookie
            }
        }
        # If NoDistributedMemoryCache is not specified, ensure a distributed memory cache is added
        if (-not $NoDistributedMemoryCache.IsPresent) {
            # Add a default in-memory distributed cache if none exists
            [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddDistributedMemoryCache($Server, $MemoryCacheOptions) | Out-Null
        }

        # Add the Session service to the server
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddSession($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
