<#
.SYNOPSIS
    Creates a new Map Route Builder for defining route mappings.
.DESCRIPTION
    The New-KrMapRouteBuilder cmdlet creates a new Map Route Builder object that can be used to define route mappings in a Kestrun server.
.PARAMETER Server
    (Optional) The Kestrun server instance to which the route mappings will be applied. If not provided, the default server will be used.
.PARAMETER Pattern
    The route pattern (e.g., '/api/items') to be mapped.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to be associated with the route. Default is GET.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Pattern '/api/items' -Verbs @('GET', 'POST')
    # Add route mappings to the Kestrun server
    # Define a route mapping
.NOTES
    This cmdlet is part of the route builder module.
#>
function New-KrMapRouteBuilder {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter()]
        [Alias('Method')]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get)
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $MapRouteBuilder = [Kestrun.Hosting.Options.MapRouteBuilder]::new($Server, $Pattern)
        $MapRouteBuilder.HttpVerbs = $Verbs
        return $MapRouteBuilder
    }
}
