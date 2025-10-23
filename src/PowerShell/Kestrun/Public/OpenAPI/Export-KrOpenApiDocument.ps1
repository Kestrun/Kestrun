<#
.SYNOPSIS
    Exports the OpenAPI document for the specified Kestrun server in the desired format.
.DESCRIPTION
    This function exports the OpenAPI document for the specified Kestrun server in either JSON or YAML format.
.PARAMETER Server
    The Kestrun server instance for which the OpenAPI document will be exported.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    The ID of the OpenAPI document to export. Default is 'default'.
.PARAMETER Format
    The format in which to export the OpenAPI document. Valid values are 'Json' and 'Yaml'. Default is 'Json'.
.PARAMETER Version
    The OpenAPI specification version to use for the export. Default is OpenApi3_1.
.EXAMPLE
    # Export the OpenAPI document for the default document ID in JSON format
    $openApiJson = Export-KrGenerateOpenApiDocument -Server $myServer -DocId 'default' -Format 'Json'
.OUTPUTS
    string representing the OpenAPI document in the specified format.
    This will be the JSON or YAML representation of the OpenAPI document.
#>
function Export-KrOpenApiDocument {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string]$DocId = 'default',
        [Parameter(Mandatory = $false)]
        [ValidateSet('Json', 'Yaml')]
        [string]$Format = 'Json',
        [Parameter(Mandatory = $false)]
        [Microsoft.OpenApi.OpenApiSpecVersion]$Version = [Microsoft.OpenApi.OpenApiSpecVersion]::OpenApi3_1
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ( -not $Server.OpenApiDocumentDescriptor.ContainsKey($DocId)) {
            throw "OpenAPI document with ID '$DocId' does not exist on the server."
        }
        if ($Format -eq 'Json') {
            return $Server.OpenApiDocumentDescriptor[$DocId].ToJson($Version)
        } else {
            return $Server.OpenApiDocumentDescriptor[$DocId].ToYaml($Version)
        }
    }
}
