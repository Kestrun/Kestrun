<#
    .SYNOPSIS
        Retrieves the context mask for a Kestrun function.
    .DESCRIPTION
        This function takes a Kestrun function and retrieves its context mask, which indicates the
        contexts in which the function is applicable (e.g., Definition, Route, Schedule).
    .PARAMETER Function
        The Kestrun function for which to retrieve the context mask.
    .OUTPUTS
        [int]
        The context mask for the specified function.
#>
function Get-KrFunctionContextMask {
    param([System.Management.Automation.FunctionInfo]$Function)

    if (-not $Function.ScriptBlock) { return 0 }

    $fnAst = $Function.ScriptBlock.Ast.
    Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $Function.Name }, $true)
    if (-not $fnAst) { return 0 }

    $attrs = @()
    if ($fnAst.Attributes) { $attrs += $fnAst.Attributes }
    if ($fnAst.Body -and $fnAst.Body.ParamBlock -and $fnAst.Body.ParamBlock.Attributes) {
        $attrs += $fnAst.Body.ParamBlock.Attributes
    }

    $kr = $attrs | Where-Object { $_.TypeName.Name -eq 'KestrunRuntimeApi' } | Select-Object -First 1
    if (-not $kr) { return 0 }

    $txt = (($kr.PositionalArguments + $kr.NamedArguments.Expression) | Where-Object { $_ }).Extent.Text
    #|           ForEach-Object { $_.Extent.Text } -join ' '
    $mask = switch ($txt) {
        "'Everywhere'" { 7 }
        "'Runtime'" { 6 }
        "'ScheduleAndDefinition'" { 5 }
        "'Definition'" { 1 }
        "'Route'" { 2 }
        "'Schedule'" { 4 }
    }

    return $mask
}

