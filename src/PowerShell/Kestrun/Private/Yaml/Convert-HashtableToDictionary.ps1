<#
.SYNOPSIS
    Convert a hashtable to an OrderedDictionary, converting any nested PSObjects to generic objects.
.DESCRIPTION
    This function takes a hashtable as input and converts it to an OrderedDictionary. It ensures that any nested PSObjects are converted to generic objects.
    The order of keys in the hashtable is preserved in the resulting OrderedDictionary.
.PARAMETER Data
    The hashtable to convert.
.EXAMPLE
    $ht = @{ "Key1" = "Value1"; "Key2" = [PSCustomObject]@{ Prop1 = "Val1"; Prop2 = "Val2" } }
    $dict = Convert-HashtableToDictionary -Data $ht
    # $dict is now an OrderedDictionary with Key1 and Key2, where Key2's value is a generic object.
.NOTES
    This function is designed to work with PowerShell 7.0 and above.
#>
function Convert-HashtableToDictionary {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Data
    )
    # Preserve original insertion order: PowerShell hashtable preserves insertion order internally
    $ordered = [System.Collections.Specialized.OrderedDictionary]::new()
    foreach ($k in $Data.Keys) {
        $ordered.Add($k, (Convert-PSObjectToGenericObject $Data[$k]))
    }
    return $ordered
}
