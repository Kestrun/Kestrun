<#
.SYNOPSIS
    Creates a new OpenAPI link and adds it to the specified OpenAPI documents.
.DESCRIPTION
    This function creates a new OpenAPI link using the provided parameters and adds it to the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI link will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the link will be added. Default is 'default'.
.PARAMETER OperationRef
    A reference to the operation in the OpenAPI document.
.PARAMETER OperationId
    The operation ID of the target operation.
.PARAMETER Description
    A description of the link.
.PARAMETER OaServer
    An OpenAPI server object representing the target server for the link.
.PARAMETER Parameters
    A hashtable of parameters for the link, where the key is the parameter name and the value is either a runtime expression (string) or a literal object.
.PARAMETER RequestBody
    The request body for the link, which can be a runtime expression (string) or a literal object (hashtable/array).
.EXAMPLE
    # Create and add a new OpenAPI link to the default document
    Add-KrOpenApiLink -OperationRef '#/paths/~1users~1{id}/get' -OperationId 'getUserById' `
        -Description 'Link to fetch user details using the id from the response body.' `
        -Parameters @{ id = '$response.body#/id' } -Server $oaServer `
        -RequestBody @{ email = '$request.body#/email'; locale = '$request.body#/locale' }
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiLink {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = @('default'),
        [Parameter(Mandatory)]
        [string]$LinkName,
        [string]$OperationRef,
        [string]$OperationId,
        [string]$Description,
        # Accept a prebuilt OpenAPI server (use New-KrOpenApiServer)
        [Microsoft.OpenApi.OpenApiServer] $OaServer,
        # Accept hashtable name -> string (runtime expression) or literal object
        [hashtable]$Parameters,
        # Accept string runtime expression or hashtable/array literal object
        [object] $RequestBody
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $link = [Kestrun.OpenApi.OpenApiLinkFactory]::Create($OperationRef, $OperationId, $Description, $OaServer, $Parameters, $RequestBody)

        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            if ($null -eq $docDescriptor.Document.Components.Links) {
                # Initialize the Links dictionary if null
                $docDescriptor.Document.Components.Links = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiLink]]::new()
            }
            if ( $docDescriptor.Document.Components.Links.ContainsKey($LinkName)) {
                Write-KrLog -Level Warning -Message "Link with name '{linkName}' already exists in OpenAPI document '{docId}'." -Values $LinkName, $DocId
            }
            $docDescriptor.Document.Components.Links.Add($LinkName, $link)
        }
    }
}
