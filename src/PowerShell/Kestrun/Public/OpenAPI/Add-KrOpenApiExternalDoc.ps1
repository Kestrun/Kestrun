<#
.SYNOPSIS
    Adds an OpenAPI External Documentation object to specified OpenAPI documents.
.DESCRIPTION
    This function adds an OpenAPI External Documentation object to the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI external documentation will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the external documentation will be added. Default is 'default'.
.PARAMETER Url
    A URI to the external documentation.
.PARAMETER Description
    A description of the external documentation.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the external documentation.
.EXAMPLE
    # Add external documentation to the default document
    Add-KrOpenApiExternalDoc -Description 'Find out more about our API here.' -Url 'https://example.com/api-docs'
    Adds external documentation with the specified description and URL to the default OpenAPI document.
.EXAMPLE
    # Add external documentation to multiple documents
    Add-KrOpenApiExternalDoc -DocId @('Default', 'v2') -Description 'API Docs' -Url 'https://example.com/docs'
    Adds external documentation with the specified description and URL to both the 'Default' and 'v2' OpenAPI documents.
.EXAMPLE
    # Add external documentation with extensions
    $extensions = [ordered]@{
        'x-doc-type' = 'comprehensive'
        'x-contact' = 'Admin Team'
    }
    Add-KrOpenApiExternalDoc -Description 'Comprehensive API docs' -Url 'https://example.com/full-api-docs' -Extensions $extensions
    Adds external documentation with the specified description, URL, and extensions to the default OpenAPI document.
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiExternalDoc {
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

        [Parameter()]
        [System.Collections.Specialized.OrderedDictionary]$Extensions
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            $docDescriptor.Document.ExternalDocs = [Kestrun.OpenApi.OpenApiDocDescriptor]::CreateExternalDocs($Url, $Description, $Extensions)
        }
    }
}
