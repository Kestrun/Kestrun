<#
.SYNOPSIS
    Adds an OpenAPI extension to specified OpenAPI documents.
.DESCRIPTION
    This function adds an OpenAPI extension to the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI extension will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the extension will be added. Default is 'default'.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add.
.EXAMPLE
    # Add an extension to the default document
    $extensions = [ordered]@{
        'x-logo' = @{
            'url' = 'https://example.com/logo.png'
            'backgroundColor' = '#FFFFFF'
            'altText' = 'Company Logo'
        }
    }
    Add-KrOpenApiExtension -Extensions $extensions
    Adds the specified extension to the default OpenAPI document.
.EXAMPLE
    # Add an extension to multiple documents
    $extensions = [ordered]@{
        'x-api-status' = 'beta'
    }
    Add-KrOpenApiExtension -DocId @('Default', 'v2') -Extensions $extensions
    Adds the specified extension to both the 'Default' and 'v2' OpenAPI documents.
 .NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiExtension {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true)]
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
            $docDescriptor.AddOpenApiExtension($Extensions)
        }
    }
}
