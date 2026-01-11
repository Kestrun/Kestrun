<#
.SYNOPSIS
    Adds external documentation to the OpenAPI document.
.DESCRIPTION
    This function adds external documentation to the OpenAPI document using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Description
    A description of the external documentation.
.PARAMETER Url
    A URI to the external documentation.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the external documentation.
.EXAMPLE
    # Add external documentation to the default document
    Add-KrOpenApiExternalDoc -Description 'Find out more about our API here.' -Url 'https://example.com/api-docs'
    Adds an external documentation link with the specified description and URL to the default OpenAPI document.
.EXAMPLE
    # Add external documentation with extensions
    $extensions = [ordered]@{
        'x-doc-type' = 'comprehensive'
        'x-contact' = 'Admin Team'
    }
    Add-KrOpenApiExternalDoc -Description 'Comprehensive API docs' -Url 'https://example.com/full-api-docs' -Extensions $extensions
    Adds an external documentation link with the specified description, URL, and extensions to the default OpenAPI document.
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiExternalDoc {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [Uri]$Url,
        [Parameter()]
        [System.Collections.Specialized.OrderedDictionary]$Extensions
    )
    # Create and add the external documentation
    $null = [Kestrun.OpenApi.OpenApiDocDescriptor]::CreateExternalDocs($Url, $Description, $Extensions)
}
