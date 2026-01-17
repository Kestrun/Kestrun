<#
.SYNOPSIS
    Creates a new OpenAPI External Documentation object.
.DESCRIPTION
    This function creates a new OpenAPI External Documentation object using the provided parameters.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI external documentation will be associated.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the external documentation will be associated. Default is 'default'.
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
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true)]
        [Uri]$Url,

        [Parameter(Mandatory = $false)]
        [string]$Description,

        [Parameter(Mandatory = $false)]
        [System.Collections.IDictionary]$Extensions
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            return $docDescriptor.CreateExternalDocs($Url, $Description, $Extensions)
        }
    }
}
