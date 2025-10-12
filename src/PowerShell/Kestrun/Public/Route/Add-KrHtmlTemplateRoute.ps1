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

    .PARAMETER AuthorizationSchema
        An optional array of authorization schemes for the route.

    .PARAMETER AuthorizationPolicy
        An optional array of authorization policies for the route.

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
        [string[]]$AuthorizationSchema = $null,

        [Parameter()]
        [string[]]$AuthorizationPolicy = $null,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {

        $options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        $options.Pattern = $Pattern
        if ($null -ne $AuthorizationSchema) {
            $Options.RequireSchemes = $AuthorizationSchema
        }
        if ($null -ne $AuthorizationPolicy) {
            $Options.RequirePolicies = $AuthorizationPolicy
        }
        if ([string]::IsNullOrWhiteSpace($HtmlTemplatePath)) {
            throw 'HtmlTemplatePath cannot be null or empty.'
        }
        # Resolve the file path relative to the Kestrun root if necessary
        $resolvedPath = Resolve-KrPath -Path $HtmlTemplatePath -KestrunRoot -Test
        Write-KrLog -Level Verbose -Message "Resolved file path: $resolvedPath"
        # Call the C# extension method to add the HTML template route
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddHtmlTemplateRoute($Server, $options, $resolvedPath) | Out-Null
        if ($PassThru) {
            return $Server
        }
    }
}
