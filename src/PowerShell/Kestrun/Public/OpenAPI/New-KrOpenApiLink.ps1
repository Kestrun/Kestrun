
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

<#
function New-RuntimeExpressionAnyWrapper {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )
    # String starting with $ is treated as runtime expression
    if ($Value -is [string] -and $Value.Trim().StartsWith('$')) {
        $w = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]::new()
        $w.Expression = [Microsoft.OpenApi.RuntimeExpression]::Build($Value)
        return $w
    }

    # Literal: build Any via C# factory (preferred) or PS reflection fallback
    $any = $null
    try { $any = [Kestrun.OpenApi.OpenApiAnyFactory]::FromObject($Value) } catch { $any = $null }
    if ($null -eq $any) { $any = ConvertTo-OpenApiAny -Value $Value }

    # Try to use ctor(IOpenApiAny) if available for stronger typing
    $wrapperType = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]
    $ctors = $wrapperType.GetConstructors()
    foreach ($ctor in $ctors) {
        $ps = $ctor.GetParameters()
        if ($ps.Count -eq 1 -and $ps[0].ParameterType.FullName -eq 'Microsoft.OpenApi.Any.IOpenApiAny') {
            try { return $ctor.Invoke(@($any)) } catch {}
        }
    }
    # Fallback to property set
    $w2 = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]::new()
    try {
        $w2.Any = $any
        return $w2
    } catch {
        # As a last resort, force a JSON string literal so we never emit type names
        try {
            $json = if ($Value -is [string]) { $Value } else { (ConvertTo-Json -InputObject $Value -Compress) }
            $w2.Any = ConvertTo-OpenApiAny -Value $json
        } catch { $w2.Any = ConvertTo-OpenApiAny -Value ([string]$Value) }
        return $w2
    }
}


function ConvertTo-OpenApiAny {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    # Resolve Any types via reflection to avoid hard PS type names
    $asm = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper].Assembly
    $ns = 'Microsoft.OpenApi.Any'
    $tObj = $asm.GetType("$ns.OpenApiObject")
    $tArr = $asm.GetType("$ns.OpenApiArray")
    $tStr = $asm.GetType("$ns.OpenApiString")

    # Hashtable -> OpenApiObject
    if ($Value -is [hashtable] -and $null -ne $tObj) {
        $obj = [System.Activator]::CreateInstance($tObj)
        foreach ($k in $Value.Keys) {
            $child = ConvertTo-OpenApiAny -Value $Value[$k]
            $obj.Add([string]$k, $child)
        }
        return $obj
    }

    # Array -> OpenApiArray
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string]) -and $null -ne $tArr) {
        $arr = [System.Activator]::CreateInstance($tArr)
        foreach ($item in @($Value)) {
            [void]$arr.Add((ConvertTo-OpenApiAny -Value $item))
        }
        return $arr
    }

    # Primitives -> OpenApiString if available; otherwise return the raw value
    if ($null -ne $tStr) {
        return [System.Activator]::CreateInstance($tStr, @([string]$Value))
    }
    return $Value
}
#>
