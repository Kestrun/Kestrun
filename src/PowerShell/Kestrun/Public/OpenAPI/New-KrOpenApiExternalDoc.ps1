<#
.SYNOPSIS
    Creates a new OpenAPI External Documentation object.
.DESCRIPTION
    This function creates a new OpenAPI External Documentation object using the provided parameters.
.PARAMETER Url
    A URI to the external documentation.
.PARAMETER Description
    A description of the external documentation.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the external documentation.
.EXAMPLE
    # Create external documentation
    $externalDoc = New-KrOpenApiExternalDoc -Description 'Find out more about our API here.' -Url 'https://example.com/api-docs'
    Creates an external documentation object with the specified description and URL.
.EXAMPLE
    # Create external documentation with extensions
    $extensions = [ordered]@{
        'x-doc-type' = 'comprehensive'
        'x-contact' = 'Admin Team'
    }
    $externalDoc = New-KrOpenApiExternalDoc -Description 'Comprehensive API docs' -Url 'https://example.com/full-api-docs' -Extensions $extensions
    Creates an external documentation object with the specified description, URL, and extensions.
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function New-KrOpenApiExternalDoc {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $true)]
        [Uri]$Url,

        [Parameter(Mandatory = $false)]
        [string]$Description,

        [Parameter(Mandatory = $false)]
        [System.Collections.Specialized.OrderedDictionary]$Extensions
    )
    # Create and add the external documentation
    return [Kestrun.OpenApi.OpenApiDocDescriptor]::CreateExternalDocs($Url, $Description, $Extensions)
}
