<#
.SYNOPSIS
    Adds a form parsing route to the Kestrun server.
.DESCRIPTION
    Registers a POST route that parses multipart/form-data payloads using
    KrFormParser. Additional request content types (e.g., multipart/mixed
    and application/x-www-form-urlencoded) are opt-in via
    KrFormOptions.AllowedContentTypes.
    Once parsed, it injects the parsed payload into the runspace as
    $FormPayload and invokes the provided script block.
.PARAMETER Pattern
    The route pattern (e.g., '/upload').
.PARAMETER ScriptBlock
    The script block to execute once the payload is parsed. The parsed
    payload is available as $FormPayload (and the request context is
    available as $Context).
.PARAMETER Options
    The KrFormOptions used to configure form parsing.
.PARAMETER OptionsName
    The name of an existing KrFormOptions on the server to use for form parsing.
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
.EXAMPLE
    Add-KrFormRoute -Pattern '/upload' -ScriptBlock {
        param($FormPayload, $Context)
        # Handle the parsed form payload
        $FormPayload.Files
    } -Options $formOptions -PassThru
    This example adds a form route at '/upload' that processes multipart/form-data uploads
    using the specified form options and returns the updated server instance.
.NOTES
    This function is part of the Kestrun.Forms module and is used to define form routes.
#>
function Add-KrFormRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [scriptblock]$ScriptBlock,

        [Parameter(ParameterSetName = 'WithOptions', Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Forms.KrFormOptions]$Options,

        [Parameter(Mandatory = $true, ParameterSetName = 'Default')]
        [string]$OptionsName,

        [Parameter()]
        [string[]]$AuthorizationScheme = $null,

        [Parameter()]
        [string[]]$AuthorizationPolicy = $null,
        [Parameter()]
        [string]$CorsPolicy,
        [Parameter()]
        [switch]$AllowAnonymous,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer
        if (-not $Server) { throw 'Server is not initialized. Call New-KrServer and Enable-KrConfiguration first.' }
    }
    process {
        if ( $PSCmdlet.ParameterSetName -eq 'Default' ) {
            $Options = $Server.GetFormOption($OptionsName)
            if ($null -eq $Options) {
                throw "Form option with name '$OptionsName' not found on the server."
            }
        }
        if ( $null -eq $Options ) {
            throw 'KrFormOptions must be provided either via Options parameter or OptionsName parameter.'
        }
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddFormRoute(
            $Server,
            $Pattern,
            $ScriptBlock,
            $Options,
            $AuthorizationScheme,
            $AuthorizationPolicy,
            $CorsPolicy,
            $AllowAnonymous.IsPresent
        ) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
