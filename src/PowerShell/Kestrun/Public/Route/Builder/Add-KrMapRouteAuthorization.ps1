<#
.SYNOPSIS
    Adds the authorization policy to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteAuthorization cmdlet adds an authorization policy to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the authorization policy will be added.
.PARAMETER AuthorizationPolicy
    An array of authorization policy names required for the route.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteAuthorization -AuthorizationPolicy 'AdminPolicy', 'UserPolicy'
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
            if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
            }
            $MapRouteBuilder.OpenApi[$verb].Enabled = $true
            if (-not $MapRouteBuilder.OpenApi[$verb].Security) {
                $MapRouteBuilder.OpenApi[$verb].Security = [System.Collections.Generic.List[
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

            $MapRouteBuilder.OpenApi[$verb].Security.Add($d) | Out-Null
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
