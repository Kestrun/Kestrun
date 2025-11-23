<#
    .SYNOPSIS
        Adds a new OpenAPI map route to the Kestrun server.
    .DESCRIPTION
        This function allows you to add a new OpenAPI map route to the Kestrun server by specifying the route path, OpenAPI specification versions, and other options.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Options
        The MapRouteOptions object to configure the route.
    .PARAMETER Pattern
        The URL path for the new OpenAPI map route.
    .PARAMETER SpecVersion
        An array of OpenAPI specification versions to support (e.g., OpenApi2_0 or OpenApi3_0).
    .PARAMETER VersionVarName
        The name of the route variable used to specify the OpenAPI version.
    .PARAMETER FormatVarName
        The name of the route variable used to specify the OpenAPI format (e.g., json or yaml).
    .PARAMETER RefreshVarName
        The name of the route variable used to trigger a refresh of the OpenAPI document.
    .PARAMETER DefaultFormat
        The default format for the OpenAPI document if not specified in the route.
    .PARAMETER DefaultVersion
        The default version for the OpenAPI document if not specified in the route.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .OUTPUTS
       [Kestrun.Hosting.KestrunHost] representing the created route.
    .EXAMPLE
        Add-KrOpenApiRoute -Server $myServer -Pattern "/openapi/{version}/{format}" -SpecVersion @('OpenApi3_0') `
            -VersionVarName "version" -FormatVarName "format" -DefaultFormat "json" -DefaultVersion "v3.0"
        Adds a new OpenAPI map route to the specified Kestrun server with the given pattern and options.
    .EXAMPLE
        Get-KrServer | Add-KrOpenApiRoute -Pattern "/openapi/{version}/{format}" -SpecVersion @('OpenApi3_0') `
            -VersionVarName "version" -FormatVarName "format" -DefaultFormat "json" -DefaultVersion "v3.0" -PassThru
        Adds a new OpenAPI map route to the specified Kestrun server with the given pattern and options.
  .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrOpenApiRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [Kestrun.Hosting.Options.MapRouteOptions]$Options,
        [Parameter()]
        [alias('Path')]
        [string]$Pattern,
        [Parameter()]
        [Microsoft.OpenApi.OpenApiSpecVersion[]]$SpecVersion,
        [Parameter()]
        [string]$VersionVarName,
        [Parameter()]
        [string]$FormatVarName,
        [Parameter()]
        [string]$RefreshVarName,
        [Parameter()]
        [string]$DefaultFormat,
        [Parameter()]
        [string]$DefaultVersion,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($null -eq $Options) {
            $Options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        }
        if (-not [string]::IsNullOrEmpty($Pattern)) {
            $Options.Pattern = $Pattern
        }
        $Options.HttpVerbs = @('GET')
        $OpenApiMapRouteOptions = [Kestrun.Hosting.Options.OpenApiMapRouteOptions]::new($Options)
        if ($PsBoundParameters.ContainsKey('DefaultFormat')) {
            $OpenApiMapRouteOptions.DefaultFormat = $DefaultFormat
        }
        if ($PsBoundParameters.ContainsKey('DefaultVersion')) {
            $OpenApiMapRouteOptions.DefaultVersion = $DefaultVersion
        }
        if ($PsBoundParameters.ContainsKey('FormatVarName')) {
            $OpenApiMapRouteOptions.FormatVarName = $FormatVarName
        }
        if ($PsBoundParameters.ContainsKey('VersionVarName')) {
            $OpenApiMapRouteOptions.VersionVarName = $VersionVarName
        }
        if ($PsBoundParameters.ContainsKey('RefreshVarName')) {
            $OpenApiMapRouteOptions.RefreshVarName = $RefreshVarName
        }
        if ($PsBoundParameters.ContainsKey('SpecVersion')) {
            $OpenApiMapRouteOptions.SpecVersions = $SpecVersion
        }

        # Call the C# extension method to add the OpenAPI map route
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddOpenApiMapRoute($Server, $OpenApiMapRouteOptions) | Out-Null

        # Return the server if PassThru is specified
        if ($PassThru) {
            return $Server
        }
    }
}
