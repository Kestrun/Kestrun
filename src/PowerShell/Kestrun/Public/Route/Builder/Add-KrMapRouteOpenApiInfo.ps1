<#
.SYNOPSIS
    Adds OpenAPI metadata to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiInfo cmdlet adds OpenAPI metadata such as summary, description, tags, and operation ID to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI metadata will be added.
.PARAMETER Summary
    (Optional) A brief summary of the route for OpenAPI documentation.
.PARAMETER Description
    (Optional) A detailed description of the route for OpenAPI documentation.
.PARAMETER Tags
    (Optional) An array of tags associated with the route for OpenAPI documentation.
.PARAMETER OperationId
    (Optional) A unique operation ID for the route in OpenAPI documentation.
.PARAMETER Deprecated
    (Optional) Indicates whether the route is deprecated in OpenAPI documentation.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiInfo -Summary 'Get Items' -Description 'Retrieves a list of items.' -Tags @('Items', 'API') -OperationId 'GetItems' -Deprecated
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiInfo {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [string]$Summary,
        [Parameter()]
        [string]$Description,
        [Parameter()]
        [string[]]$Tags,
        [Parameter()]
        [string]$OperationId,
        [switch]$Deprecated
    )
    process {
        $MapRouteBuilder.OpenApi.Enabled = $true
        $MapRouteBuilder.OpenApi.Deprecated = $Deprecated.IsPresent
        if ($PsBoundParameters.ContainsKey('Summary')) {
            $MapRouteBuilder.OpenApi.Summary = $Summary
        }
        if ($PsBoundParameters.ContainsKey('Description')) {
            $MapRouteBuilder.OpenApi.Description = $Description
        }
        if ($PsBoundParameters.ContainsKey('Tags')) {
            $MapRouteBuilder.OpenApi.Tags = $Tags
        }
        if ($PsBoundParameters.ContainsKey('OperationId')) {
            $MapRouteBuilder.OpenApi.OperationId = $OperationId
        }

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
