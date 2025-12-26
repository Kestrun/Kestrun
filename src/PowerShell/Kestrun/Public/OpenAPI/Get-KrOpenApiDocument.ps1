<#
.SYNOPSIS
    Retrieves the OpenAPI document for the specified Kestrun server.
.DESCRIPTION
    This function retrieves the OpenAPI document for the specified Kestrun server using the discovered components.
.PARAMETER Server
    The Kestrun server instance for which the OpenAPI document will be retrieved.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    The ID of the OpenAPI document to retrieve. Default is 'default'.
.PARAMETER Version
    The OpenAPI specification version to use for retrieval. Default is OpenApi3_1.
.PARAMETER Yaml
    If specified, the function will return the OpenAPI document in YAML format.
.PARAMETER Json
    If specified, the function will return the OpenAPI document in JSON format.
    If neither Yaml nor Json is specified, the function will return the document as a PowerShell ordered hashtable.
.EXAMPLE
    # Retrieve the OpenAPI document as a hashtable
    $openApiDoc = Get-KrOpenApiDocument -Server $myServer -DocId 'default'
.EXAMPLE
    # Retrieve the OpenAPI document in JSON format
    $openApiJson = Get-KrOpenApiDocument -Server $myServer -Doc Id 'default' -Json
.EXAMPLE
    # Retrieve the OpenAPI document in YAML format
    $openApiYaml = Get-KrOpenApiDocument -Server $myServer -DocId 'default' -Yaml
.OUTPUTS
    [string] (if Yaml or Json is specified)
    [System.Management.Automation.OrderedHashtable] (if neither Yaml nor Json is specified)
#>
function Get-KrOpenApiDocument {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'HashTable')]
    [OutputType([string])]
    [OutputType([System.Management.Automation.OrderedHashtable])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationId,

        [Parameter()]
        [Microsoft.OpenApi.OpenApiSpecVersion]$Version = [Microsoft.OpenApi.OpenApiSpecVersion]::OpenApi3_1,

        [Parameter(ParameterSetName = 'Yaml')]
        [switch]$Yaml,

        [Parameter(ParameterSetName = 'Json')]
        [switch]$Json
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ( -not $Server.OpenApiDocumentDescriptor.ContainsKey($DocId)) {
            throw "OpenAPI document with ID '$DocId' does not exist on the server."
        }
        # Log the start of the validation process
        Write-KrLog -Level Information -Message "Starting OpenAPI document retrieval for DocId: '{DocId}' Version: '{Version}'" -Values $DocId, $Version
        # Retrieve the document descriptor
        $doc = $Server.OpenApiDocumentDescriptor[$DocId]
        if ( $Yaml.IsPresent) {
            return $doc.ToYaml($Version)
        } elseif ( $Json.IsPresent) {
            return $doc.ToJson($Version)
        }
        return $doc.ToJson($Version) | ConvertFrom-Json -AsHashtable
    }
}
