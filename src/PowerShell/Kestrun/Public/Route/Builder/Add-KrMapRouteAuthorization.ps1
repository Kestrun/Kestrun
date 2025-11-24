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
        if ($Verbs.Count -eq 0) {
            # Apply to all verbs defined in the MapRouteBuilder
            $Verbs = $MapRouteBuilder.HttpVerbs
        }
        foreach ($verb in $Verbs) {
            if (-not $MapRouteBuilder.OpenAPI.ContainsKey($verb)) {
                $MapRouteBuilder.OpenAPI[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
            }
            $MapRouteBuilder.OpenAPI[$verb].Enabled = $true
            if (-not $MapRouteBuilder.OpenAPI[$verb].Security) {
                $MapRouteBuilder.OpenAPI[$verb].Security = [System.Collections.Generic.List[
                System.Collections.Generic.Dictionary[
                string,
                System.Collections.Generic.IEnumerable[string]
                ]
                ]]::new()
            }
            [string[]]$schemas = @()
            # d: Dictionary<string, IEnumerable<string>>
            $d = [System.Collections.Generic.Dictionary[
            string,
            System.Collections.Generic.IEnumerable[string]
            ]]::new()

            if (-not ([string]::IsNullOrWhiteSpace($Schema))) {
                $schemas += $Schema
                $d[$Schema] = [System.Collections.Generic.List[string]]::new()
            }

            # one list of scopes for this schema
            foreach ($p in $Policy) {
                $tmpSchema = $MapRouteBuilder.Server.RegisteredAuthentications.GetSchemesByPolicy($p)
                foreach ($sc in $tmpSchema) {
                    if (-not $d.ContainsKey($sc)) {
                        # already have this schema, skip adding again
                        $d[$sc] = [System.Collections.Generic.List[string]]::new()
                    }
                    $d[$sc].Add($p) | Out-Null
                    if (-not $schemas.Contains($sc)) {
                        $schemas += $sc
                    }
                }
            }

            $MapRouteBuilder.OpenAPI[$verb].Security.Add($d) | Out-Null
        }
        if ($schemas.Count -gt 0) {
            # Add to the authorization requirements
            $MapRouteBuilder.RequireSchemes.AddRange($schemas) | Out-Null
        }
        # Add policies if any
        if ($Policy.Count -gt 0) {
            $MapRouteBuilder.RequirePolicies.AddRange($Policy) | Out-Null
        }

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
