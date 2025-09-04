<#
    .SYNOPSIS
        Adds a new HTML template route to the Kestrun server.

    .DESCRIPTION
        This function allows you to add a new HTML template route to the Kestrun server by specifying the route path and the HTML template file path.

    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.

    .PARAMETER Pattern
        The URL path for the new route.

    .PARAMETER HtmlTemplatePath
        The file path to the HTML template to be used for the route.

    .PARAMETER Authorization
        An optional array of authorization strings for the route.

    .PARAMETER PassThru
        If specified, the function will return the created route object.

    .OUTPUTS
        [Microsoft.AspNetCore.Builder.IEndpointConventionBuilder] representing the created route.

    .EXAMPLE
        Add-KrHtmlTemplateRoute -Server $myServer -Path "/myroute" -HtmlTemplatePath "C:\Templates\mytemplate.html"
        Adds a new HTML template route to the specified Kestrun server with the given path and template file.

    .EXAMPLE
        Get-KrServer | Add-KrHtmlTemplateRoute -Path "/myroute" -HtmlTemplatePath "C:\Templates\mytemplate.html" -PassThru
        Adds a new HTML template route to the current Kestrun server and returns the route object
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrHtmlTemplateRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [alias('Path')]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$HtmlTemplatePath,

        [Parameter()]
        [string[]]$Authorization = $null,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        $options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        $options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]]::new([Kestrun.Utilities.HttpVerb]::Get)
        $options.Pattern = $Pattern
        $options.RequireAuthorization = $Authorization

        [Kestrun.Hosting.KestrunHostMapExtensions]::AddHtmlTemplateRoute($Server, $options, $HtmlTemplatePath) | Out-Null
        if ($PassThru) {
            return $Server
        }
    }
}

