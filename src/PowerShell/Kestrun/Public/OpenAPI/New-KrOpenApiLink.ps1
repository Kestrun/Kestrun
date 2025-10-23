
function New-KrOpenApiLink {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [string]$OperationRef,
        [string]$OperationId,
        [string]$Description,
        # Accept a prebuilt OpenAPI server (use New-KrOpenApiServer)
        [Microsoft.OpenApi.OpenApiServer] $Server,
        # Accept hashtable name -> string (runtime expression) or literal object
        [hashtable]$Parameters,
        # Accept string runtime expression or hashtable/array literal object
        [object] $RequestBody
    )

    return [Kestrun.OpenApi.OpenApiLinkFactory]::Create($OperationRef, $OperationId, $Description, $Server, $Parameters, $RequestBody)
}


 
