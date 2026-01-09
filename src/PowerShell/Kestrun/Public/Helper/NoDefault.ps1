<#
.SYNOPSIS
    Placeholder function to indicate no default value.
.DESCRIPTION
    This function serves as a marker to indicate that no default value is provided for a parameter.
    It returns $null when invoked.
    When used in parameter declarations, it allows the caller to distinguish between an explicit
    default value and the absence of a default.
.PARAMETER Value
    An optional value to return instead of the sentinel indicating no default.
    If provided, this value is returned immediately.
.EXAMPLE
    Usage example:
        function Test-Function {
            param(
                [datetime]$DateParam = (NoDefault)
            )
            if ($DateParam -eq [datetime]::MinValue) {
                Write-Output "No default provided for DateParam."
            } else {
                Write-Output "DateParam has value: $DateParam"
            }
        }
.NOTES
    When a parameter is declared with NoDefault as its default value, the function inspects
    the call stack to determine the static type of the parameter. If the type is a nullable
    type or a reference type, it returns $null. For non-nullable value types, it returns a sentinel
    value (e.g., [datetime]::MinValue for [datetime]) that can be detected by the caller.
    This allows functions to differentiate between parameters that have no default and those
    that have an explicit default value.
.OUTPUTS
    Returns $null.
#>
function NoDefault {
    param(
        [object] $Value = $null
    )

    # If caller provided a real default, return it immediately.
    # Usage: [datetime]$x = (NoDefault ([datetime]'2026-01-01'))
    if ($PSBoundParameters.ContainsKey('Value')) {
        return $Value
    }

    $call = (Get-PSCallStack)[1]
    if (-not $call.ScriptName) { return $null }

    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $call.ScriptName, [ref]$tokens, [ref]$errors
    )

    $line = $call.ScriptLineNumber

    # Find the assignment ON THIS LINE whose RHS is NoDefault / (NoDefault) / $(NoDefault)
    $assign = $ast.FindAll({
            param($a)
            $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and
            $a.Extent.StartLineNumber -le $line -and
            $a.Extent.EndLineNumber -ge $line -and
            ($a.Right.Extent.Text -match '^\s*\(?\s*NoDefault\b')
        }, $true) | Select-Object -First 1

    if (-not $assign) { return $null }

    # In script scope, LHS is typically an AttributedExpressionAst
    $lhs = $assign.Left

    # Prefer the inner attributed child if present (matches what you printed)
    $attrib = $lhs.Child
    if ($attrib -isnot [System.Management.Automation.Language.AttributedExpressionAst]) {
        # fallback: maybe LHS itself is attributed
        if ($lhs -is [System.Management.Automation.Language.AttributedExpressionAst]) {
            $attrib = $lhs
        } else {
            return $null
        }
    }

    switch ($attrib.Attribute.TypeName.name) {
        'ValidateSet' {
            $defaultNull = ($attrib.Attribute.Extent.Text -replace ".*ValidateSet\(|['\)]", '').Split(',')[0].Trim()
            $attrib = $attrib.Child
            break
        }
        'ValidateRange' {
            $defaultNull = ($attrib.Attribute.Extent.Text -replace '.*ValidateRange\(|[\]]', '').Split(',')[0].Trim()
            $attrib = $attrib.Child
            break
        }
        { $_ -in 'ValidateNotNull', 'ValidateNotNullOrWhiteSpace', 'ValidateNotNullOrEmpty' } {
            $defaultNull = '__FAKE_NULL__'
            $attrib = $attrib.Child
            break
        }
        default { $defaultNull = $null }
    }

    $t = $attrib.StaticType

    # Nullable<T> => return $null
    if ($t.IsGenericType -and $t.GetGenericTypeDefinition() -eq [Nullable`1]) {
        return $defaultNull
    }

    # Reference types => return $null
    if (-not $t.IsValueType) {
        return $defaultNull
    }
    # Nullable reference types => return $null
    if ($null -ne $defaultNull) {
        return $defaultNull
    }
    # Non-nullable value types => return a sentinel you can detect later
    switch ($t.FullName) {
        'System.DateTime' { return [datetime]::MinValue }
        'System.DateTimeOffset' { return [datetimeoffset]::MinValue }
        'System.Guid' { return [guid]::Empty }
        'System.TimeSpan' { return [timespan]::Zero }
        default {
            # int => 0, bool => false, etc.
            return [Activator]::CreateInstance($t)
        }
    }
}

