<#
.SYNOPSIS
    Adds HTTP verbs and route pattern to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteVerbPattern cmdlet adds specified HTTP verbs and a route pattern to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the HTTP verbs and route pattern will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to be associated with the route. Default is GET.
.PARAMETER Pattern
    The route pattern (e.g., '/api/items') to be mapped.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    # Add HTTP verbs and route pattern
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteVerbPattern {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Alias('Method')]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get),
        [Parameter(Mandatory = $true)]
        [string]$Pattern

    )
    process {
        $MapRouteBuilder.HttpVerbs = $Verbs
        $MapRouteBuilder.Pattern = $Pattern

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
