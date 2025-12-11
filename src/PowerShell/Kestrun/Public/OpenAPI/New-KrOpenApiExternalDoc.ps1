<#
.SYNOPSIS
    Creates a new OpenAPI external documentation object.
.DESCRIPTION
    This function creates a new OpenAPI external documentation object using the provided parameters.
.PARAMETER Description
    A description of the external documentation.
.PARAMETER Url
    A URI to the external documentation.
.EXAMPLE
    $externalDocs = New-KrOpenApiExternalDoc -Description 'Find out more about our API here.' -Url 'https://example.com/api-docs'
.OUTPUTS
    Microsoft.OpenApi.OpenApiExternalDocs
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function New-KrOpenApiExternalDoc {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([Microsoft.OpenApi.OpenApiExternalDocs])]
    param(
        [Parameter()]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [Uri]$Url
    )

    $externalDocs = [Microsoft.OpenApi.OpenApiExternalDocs]::new()

    if ($PsBoundParameters.ContainsKey('Description')) {
        $externalDocs.Description = $Description
    }
    $externalDocs.Url = $Url

    return $externalDocs
}
