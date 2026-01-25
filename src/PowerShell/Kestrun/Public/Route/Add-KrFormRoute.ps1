<#
    .SYNOPSIS
        Adds a form parsing route to the Kestrun server.
    .DESCRIPTION
        Registers a POST route that parses multipart/form-data, multipart/mixed,
        and application/x-www-form-urlencoded payloads using KrFormParser, then
        injects the parsed payload into the runspace as $FormPayload and invokes
        the provided script block.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
    .PARAMETER Pattern
        The route pattern (e.g., '/upload').
    .PARAMETER ScriptBlock
        The script block to execute once the payload is parsed. The parsed
        payload is available as $FormPayload (and the request context is
        available as $Context).
    .PARAMETER Options
        The KrFormOptions used to configure form parsing.
    .PARAMETER AuthorizationScheme
        Optional authorization schemes required for the route.
    .PARAMETER AuthorizationPolicy
        Optional authorization policies required for the route.
    .PARAMETER CorsPolicy
        Optional CORS policy name to apply to the route.
    .PARAMETER AllowAnonymous
        Allows anonymous access to the route.
    .PARAMETER PassThru
        Returns the updated server instance when specified.
#>

function Add-KrFormRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [scriptblock]$ScriptBlock,
        [Kestrun.Forms.KrFormOptions]$Options,
        [string[]]$AuthorizationScheme = $null,
        [string[]]$AuthorizationPolicy = $null,
        [string]$CorsPolicy,
        [switch]$AllowAnonymous,
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($null -eq $Options) {
            $Options = [Kestrun.Forms.KrFormOptions]::new()
        }
        if ($null -eq $Options.Logger) {
            $Options.Logger = $Server.Logger
        }

        $wrapperContent = @'
##############################
# Form Route Wrapper
##############################
$FormPayload = [Kestrun.Forms.KrFormParser]::Parse($Context.HttpContext, $Options, $Context.Ct)

############################
# User Scriptblock
############################

'@ + $ScriptBlock.ToString()

        # Combine the wrapper and user scriptblocks
        $wrapper = [scriptblock]::Create($wrapperContent)

        # Register the route
        Add-KrMapRoute -Server $Server -Verbs Post -Pattern $Pattern -Arguments @{ 'Options' = $Options } -ScriptBlock $wrapper `
            -AuthorizationScheme $AuthorizationScheme -AuthorizationPolicy $AuthorizationPolicy `
            -CorsPolicy $CorsPolicy -AllowAnonymous:$AllowAnonymous | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
