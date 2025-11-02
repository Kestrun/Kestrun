<#
.SYNOPSIS
    Builds the OpenAPI document for the specified Kestrun server.
.DESCRIPTION
    This function builds the OpenAPI document for the specified Kestrun server using the discovered components.
.PARAMETER Server
    The Kestrun server instance for which the OpenAPI document will be built.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    The ID of the OpenAPI document to build. Default is 'default'.
.EXAMPLE
    # Build the OpenAPI document for the default document ID
    Build-KrOpenApiDocument -Server $myServer -DocId 'default'
.OUTPUTS
    Kestrun.OpenApi.OpenApiDocumentDescriptor
#>
function Build-KrOpenApiDocument {
    [KestrunRuntimeApi('Everywhere')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string]$DocId = 'default'
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        Write-KrLog -Level Information -Logger $Server.Logger -Message 'Building OpenAPI document...'
    }
    process {
        if ( -not $Server.OpenApiDocumentDescriptor.ContainsKey($DocId)) {
            throw "OpenAPI document with ID '$DocId' does not exist on the server."
        }
        Get-KrAnnotatedFunctionsLoaded
        $doc = $Server.OpenApiDocumentDescriptor[$DocId]
        $doc.GenerateDoc()
    }
}
