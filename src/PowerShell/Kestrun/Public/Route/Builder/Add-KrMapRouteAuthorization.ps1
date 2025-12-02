<#
.SYNOPSIS
    Adds the authorization policy to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteAuthorization cmdlet adds an authorization policy to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the authorization policy will be added.
.PARAMETER Policy
    An array of authorization policy names required for the route.
.PARAMETER Verbs
    An array of HTTP verbs to which the authorization policy will be applied.
    If not specified, the policy will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Schema
    The authentication schema to be used for the authorization.
.OUTPUTS
    [Kestrun.Hosting.Options.MapRouteBuilder] representing the modified Map Route Builder with the added authorization policy.
.EXAMPLE
    $mapRouteBuilder = New-KrMapRouteBuilder -Server $server -Pattern "/api/secure" -HttpVerbs @("GET", "POST")
    $updatedMapRouteBuilder = $mapRouteBuilder | Add-KrMapRouteAuthorization -Policy "AdminPolicy" -Verbs @("GET") -Schema "Bearer"
    This example creates a new Map Route Builder for the "/api/secure" pattern with GET and POST verbs,
    then adds an authorization policy named "AdminPolicy" for the GET verb using the "Bearer" authentication schema.
.EXAMPLE
    $mapRouteBuilder = New-KrMapRouteBuilder -Server $server -Pattern "/api/secure" -HttpVerbs @("GET", "POST")
    $updatedMapRouteBuilder = $mapRouteBuilder | Add-KrMapRouteAuthorization -Policy "UserPolicy"
    This example creates a new Map Route Builder for the "/api/secure" pattern with GET and POST verbs,
    then adds an authorization policy named "UserPolicy" for all defined verbs.
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteAuthorization {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter( )]
        [string]$Schema,
        [Parameter()]
        [Alias('Scope')]
        [string[]]$Policy
    )
    process {
        return $MapRouteBuilder.AddAuthorization(
            $Policy, $Verbs, $Schema
        )
    }
}
