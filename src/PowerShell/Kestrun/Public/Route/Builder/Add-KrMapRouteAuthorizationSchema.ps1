<#
.SYNOPSIS
    Adds the authorization schema to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteAuthorizationSchema cmdlet adds authorization schema names to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the authorization schema will be added.
.PARAMETER AuthorizationSchema
    An array of authorization schema names required for the route.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteAuthorizationSchema -AuthorizationSchema 'Basic', 'Bearer'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteAuthorizationSchema {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(Mandatory = $true)]
        [string[]]$AuthorizationSchema
    )
    process {
        $MapRouteBuilder.RequireSchemes = $AuthorizationSchema

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
