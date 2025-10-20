
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

    $link = [Microsoft.OpenApi.OpenApiLink]::new()
    if ($PSBoundParameters.ContainsKey('OperationRef')) { $link.OperationRef = $OperationRef }
    if ($PSBoundParameters.ContainsKey('OperationId')) { $link.OperationId = $OperationId }
    if ($PSBoundParameters.ContainsKey('Description')) { $link.Description = $Description }

    if ($PSBoundParameters.ContainsKey('Server') -and $null -ne $Server) { $link.Server = $Server }

    if ($PSBoundParameters.ContainsKey('Parameters') -and $null -ne $Parameters) {
        $dictType = 'System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.RuntimeExpressionAnyWrapper]'
        $paramDict = New-Object $dictType
        foreach ($k in $Parameters.Keys) {
            $v = $Parameters[$k]
            $paramDict[$k] = New-RuntimeExpressionAnyWrapper -Value $v
        }
        $link.Parameters = $paramDict
    }

    if ($PSBoundParameters.ContainsKey('RequestBody') -and $null -ne $RequestBody) {
        $link.RequestBody = New-RuntimeExpressionAnyWrapper -Value $RequestBody
    }

    return $link
}

function New-RuntimeExpressionAnyWrapper {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )
    $wrapper = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]::new()

    # String starting with $ is treated as runtime expression
    if ($Value -is [string] -and $Value.Trim().StartsWith('$')) {
        $wrapper.Expression = [Microsoft.OpenApi.RuntimeExpression]::Build($Value)
        return $wrapper
    }

    # Otherwise, convert to OpenApiAny (object/array/primitive)
    $wrapper.Any = ConvertTo-OpenApiAny -Value $Value
    return $wrapper
}


function ConvertTo-OpenApiAny {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    # Hashtable -> OpenApiObject
    if ($Value -is [hashtable]) {
        $obj = [Microsoft.OpenApi.Any.OpenApiObject]::new()
        foreach ($k in $Value.Keys) {
            $obj.Add([string]$k, (ConvertTo-OpenApiAny -Value $Value[$k]))
        }
        return $obj
    }

    # Array -> OpenApiArray
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $arr = [Microsoft.OpenApi.Any.OpenApiArray]::new()
        foreach ($item in @($Value)) {
            [void]$arr.Add((ConvertTo-OpenApiAny -Value $item))
        }
        return $arr
    }

    # Primitives -> OpenApiString (simple, safe)
    return [Microsoft.OpenApi.Any.OpenApiString]::new([string]$Value)
}
