<#
.SYNOPSIS
    Adds an OpenAPI parameter to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiParameter cmdlet adds an OpenAPI parameter with an optional description and reference to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI parameter will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI parameter will be applied. If not specified, the parameter will be applied to all verbs defined in the Map Route Builder.
.PARAMETER DocId
    The documentation ID associated with the OpenAPI parameter. Default is the default authentication scheme name.
.PARAMETER Description
    A description of the OpenAPI parameter.
.PARAMETER ReferenceId
    A reference string for the OpenAPI parameter.
.PARAMETER Embed
    A switch indicating whether to embed the parameter definition directly into the route or to reference it.
.PARAMETER Key
    An optional key to set the name of the parameter in the OpenAPI definition.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiParameter -Description 'Item ID parameter' -ReferenceId 'ItemIdParam'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiParameter {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter()]
        [string]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName,
        [Parameter()]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [string]$ReferenceId,
        [Parameter()]
        [switch]$Embed,
        [Parameter()]
        [string]$Key
    )
    process {
        # Determine if we are using path-level metadata
        $usePathLevel = $Verbs.Count -eq 0

        # Use reference-based parameter
        $parameters = $MapRouteBuilder.Server.OpenApiDocumentDescriptor[$DocId].Document.Components.Parameters
        if (-not $parameters.ContainsKey($ReferenceId)) {
            throw "Parameter with ReferenceId '$ReferenceId' does not exist in the OpenAPI document components."
        }
        if ($Embed) {
            $parameter = ([Kestrun.OpenApi.OpenApiComponentClone]::Clone($parameters[$ReferenceId]))
            # Set parameter name if provided
            if ($PSBoundParameters.ContainsKey('Key')) {
                $parameter.Name = $Key
            }
        } else {
            $parameter = ([Microsoft.OpenApi.OpenApiParameterReference]::new($ReferenceId))
        }
        # Update the MapRouteBuilder to use path-level metadata
        if ($usePathLevel) {
            if ($null -eq $MapRouteBuilder.PathLevelOpenAPIMetadata) {
                $MapRouteBuilder.PathLevelOpenAPIMetadata = [Kestrun.Hosting.Options.OpenAPICommonMetadata]::new()
            }
            $MapRouteBuilder.PathLevelOpenAPIMetadata.Parameters.Add($parameter)
        } else {
            # Add to specified verbs
            foreach ($verb in $Verbs) {
                if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                    $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
                }
                # Ensure the MapRouteBuilder is marked as having OpenAPI metadata
                $MapRouteBuilder.OpenApi[$verb].Enabled = $true

                # Add description if provided
                if ($PSBoundParameters.ContainsKey('Description')) {
                    $parameter.Description = $Description
                }
                # Add the parameter to the MapRouteBuilder
                $MapRouteBuilder.OpenApi[$verb].Parameters.Add($parameter)
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
