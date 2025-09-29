<#
.SYNOPSIS
    Recursively normalizes a value into a form suitable for Dictionary[string, object].
.DESCRIPTION
    Recursively normalizes a value into a form suitable for Dictionary[string, object].
    - Unwraps PSObject shells
    - Converts IDictionary to Dictionary[string, object]
    - Converts IEnumerable (except string) to List[object]
    - Primitives and POCOs are returned as-is
.PARAMETER Value
    The value to normalize.
.PARAMETER Depth
    Current recursion depth to prevent infinite loops.
.PARAMETER MaxRecursionDepth
    Maximum recursion depth. Defaults to 8.
.OUTPUTS
    The normalized value or $null if the input is $null or could not be normalized.
.NOTES
    Limits recursion depth to 8 levels.
#>
function _NormalizeValueToDictionary([object]$Value, [int]$Depth, [int]$MaxRecursionDepth = 8) {
    if ($null -eq $Value) { return $null }
    if ($Depth -gt $MaxRecursionDepth) { return ($Value.ToString()) }

    # Unwrap PSObject shell
    if ($Value -is [System.Management.Automation.PSObject]) {
        $base = $Value.BaseObject
        if ($null -eq $base -or $base -eq $Value) { return $Value.ToString() }
        return _NormalizeValue $base ($Depth + 1)
    }

    # Hashtable / IDictionary → new Dictionary[string, object]
    if ($Value -is [System.Collections.IDictionary]) {
        $out = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($key in $Value.Keys) {
            if ([string]::IsNullOrWhiteSpace([string]$key)) { continue }
            $nv = _NormalizeValue $Value[$key] ($Depth + 1)
            if ($null -ne $nv) { $out[[string]$key] = $nv }
        }
        return $out
    }

    # Enumerable (but not string) → List<object>
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $list = New-Object System.Collections.Generic.List[object]
        foreach ($item in $Value) { $list.Add((_NormalizeValue $item ($depth + 1))) }
        return $list
    }

    return $Value  # primitive / POCO
}
