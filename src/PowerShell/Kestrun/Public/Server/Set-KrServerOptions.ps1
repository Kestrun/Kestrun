<#
    .SYNOPSIS
        Configures advanced options and operational limits for a Kestrun server instance.
    .DESCRIPTION
        The Set-KrServerOptions function allows fine-grained configuration of a Kestrun server instance.
        It enables administrators to control server behavior, resource usage, and protocol compliance by
        setting limits on request sizes, connection counts, timeouts, and other operational parameters.
        Each parameter is optional and, if not specified, the server will use its built-in default value.
    .PARAMETER Server
        The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.
    .PARAMETER AllowSynchronousIO
        If set to $true, allows synchronous IO operations on the server.
        Synchronous IO can impact scalability and is generally discouraged.
        Default: $false.
    .PARAMETER DisableResponseHeaderCompression
        If set to $true, disables compression of HTTP response headers.
        Default: $false.
    .PARAMETER DenyServerHeader
        If set to $true, removes the 'Server' HTTP header from responses for improved privacy and security.
        Default: $false.
    .PARAMETER AllowAlternateSchemes
        If set to $true, allows alternate URI schemes (other than HTTP/HTTPS) in requests.
        Default: $false.
    .PARAMETER AllowHostHeaderOverride
        If set to $true, permits overriding the Host header in incoming requests.
        Default: $false.
    .PARAMETER DisableStringReuse
        If set to $true, disables internal string reuse optimizations, which may increase memory usage but can help with certain debugging scenarios.
        Default: $false.
    .PARAMETER MaxRunspaces
        Specifies the maximum number of runspaces to use for script execution.
        This can help control resource usage and concurrency in script execution.
        Default: 2x CPU cores or as specified in the KestrunOptions.
    .PARAMETER MinRunspaces
        Specifies the minimum number of runspaces to use for script execution.
        This ensures that at least a certain number of runspaces are always available for processing requests.
        Default: 1.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after applying the limits.
    .EXAMPLE
        Set-KrServerOptions -Server $srv -MaxRequestBodySize 1000000
        Configures the server instance $srv to limit request body size to 1,000,000 bytes.
    .EXAMPLE
        Set-KrServerOptions -Server $srv -AllowSynchronousIO
        Configures the server instance $srv to allow synchronous IO operations.
    .NOTES
        All parameters are optional except for -Server.
        Defaults are based on typical Kestrun server settings as of the latest release.
#>
function Set-KrServerOptions {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [switch]$AllowSynchronousIO,
        [Parameter()]
        [switch]$DisableResponseHeaderCompression ,
        [Parameter()]
        [switch]$DenyServerHeader,
        [Parameter()]
        [switch]$AllowAlternateSchemes,
        [Parameter()]
        [switch]$AllowHostHeaderOverride,
        [Parameter()]
        [switch]$DisableStringReuse,
        [Parameter()]
        [int]$MaxRunspaces,
        [Parameter()]
        [int]$MinRunspaces = 1,
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
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }

        if ($AllowSynchronousIO.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.AllowSynchronousIO to {AllowSynchronousIO}" -Values $AllowSynchronousIO.IsPresent
            $options.ServerOptions.AllowSynchronousIO = $AllowSynchronousIO.IsPresent
        }
        if ($DisableResponseHeaderCompression.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.AllowResponseHeaderCompression to {AllowResponseHeaderCompression}" `
                -Values $false
            $options.ServerOptions.AllowResponseHeaderCompression = $false
        }
        if ($DenyServerHeader.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.AddServerHeader to {AddServerHeader}" -Values $false
            $options.ServerOptions.AddServerHeader = $false
        }
        if ($AllowAlternateSchemes.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.AllowAlternateSchemes to {AllowAlternateSchemes}" -Values $true
            $options.ServerOptions.AllowAlternateSchemes = $true
        }
        if ($AllowHostHeaderOverride.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.AllowHostHeaderOverride to {AllowHostHeaderOverride}" -Values $true
            $options.ServerOptions.AllowHostHeaderOverride = $true
        }
        if ($DisableStringReuse.IsPresent) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting ServerOptions.DisableStringReuse to {DisableStringReuse}" -Values $true
            $options.ServerOptions.DisableStringReuse = $true
        }
        if ($MaxRunspaces -gt 0) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting MaxRunspaces to {MaxRunspaces}" -Values $MaxRunspaces
            $options.MaxRunspaces = $MaxRunspaces
        }
        if ($MinRunspaces -gt 0) {
            Write-KrLog -Logger $Server.Logger -Level Verbose -Message "Setting MinRunspaces to {MinRunspaces}" -Values $MinRunspaces
            $options.MinRunspaces = $MinRunspaces
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

