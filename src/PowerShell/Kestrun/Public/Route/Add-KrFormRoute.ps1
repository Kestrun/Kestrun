<#
    .SYNOPSIS
        Adds a form parsing route to the Kestrun server.
    .DESCRIPTION
        Registers a POST route that parses multipart/form-data payloads using
        KrFormParser. Additional request content types (e.g., multipart/mixed
        and application/x-www-form-urlencoded) are opt-in via
        KrFormOptions.AllowedRequestContentTypes.

        Once parsed, it injects the parsed payload into the runspace as
        $FormPayload and invokes the provided script block.
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
        [string]$OpenApiOperationId,
        [string[]]$OpenApiTags,
        [string]$OpenApiSummary,
        [string]$OpenApiDescription,
        [string[]]$OpenApiDocumentId,
        [switch]$DisableOpenApi,
        [switch]$AllowAnonymous,
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddFormRoute(
            $Server,
            $Pattern,
            $ScriptBlock,
            $Options,
            $AuthorizationScheme,
            $AuthorizationPolicy,
            $CorsPolicy,
            $AllowAnonymous.IsPresent,
            $OpenApiOperationId,
            $OpenApiTags,
            $OpenApiSummary,
            $OpenApiDescription,
            $OpenApiDocumentId,
            $DisableOpenApi.IsPresent
        ) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
