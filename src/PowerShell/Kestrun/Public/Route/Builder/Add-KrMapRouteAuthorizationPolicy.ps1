<#
.SYNOPSIS
    Adds the authorization policy to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteAuthorizationPolicy cmdlet adds an authorization policy to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the authorization policy will be added.
.PARAMETER AuthorizationPolicy
    An array of authorization policy names required for the route.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteAuthorizationSchema -AuthorizationPolicy 'AdminPolicy', 'UserPolicy'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteAuthorizationPolicy {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(Mandatory = $true)]
        [string[]]$AuthorizationPolicy
    )
    process {
        $MapRouteBuilder.RequirePolicies = $AuthorizationPolicy

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
