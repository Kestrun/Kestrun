 
function New-KrOpenApiLink {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [string]$OperationRef,
        [string]$OperationId,
        [string]$Description,
        [Microsoft.OpenApi.OpenApiServer] $Server,
        [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.RuntimeExpressionAnyWrapper]]$Parameters,
        [Microsoft.OpenApi.RuntimeExpressionAnyWrapper] $RequestBody
    )
    $server = [Microsoft.OpenApi.OpenApiLink]::new()
    if ($PsBoundParameters.ContainsKey('OperationRef')) {
        $server.OperationRef = $OperationRef
    }
    if ($PsBoundParameters.ContainsKey('OperationId')) {
        $server.OperationId = $OperationId
    }
    if ($PsBoundParameters.ContainsKey('Description')) {
        $server.Description = $Description
    }
    if ($PsBoundParameters.ContainsKey('Server')) {
        $server.Server = $Server
    }
    if ($PsBoundParameters.ContainsKey('Parameters')) {
        $server.Parameters = $Parameters
    }
    if ($PsBoundParameters.ContainsKey('RequestBody')) {
        $server.RequestBody = $RequestBody
    }
    return $server
}
