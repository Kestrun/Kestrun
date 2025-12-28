<#
.SYNOPSIS
    Adds external documentation to the OpenAPI document.
.DESCRIPTION
    This function adds external documentation to the OpenAPI document using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI external documentation will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the external documentation will be added. Default is 'default'.
.PARAMETER Description
    A description of the external documentation.
.PARAMETER Url
    A URI to the external documentation.
.EXAMPLE
    # Add external documentation to the default document
    Add-KrOpenApiExternalDoc -Description 'Find out more about our API here.' -Url 'https://example.com/api-docs'
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
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [Uri]$Url
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            $docDescriptor.Document.ExternalDocs = [Microsoft.OpenApi.OpenApiExternalDocs]::new()

            if ($PsBoundParameters.ContainsKey('Description')) {
                $docDescriptor.Document.ExternalDocs.Description = $Description
            }
            if ($PsBoundParameters.ContainsKey('Url')) {
                $docDescriptor.Document.ExternalDocs.Url = $Url
            }
        }
    }
}
