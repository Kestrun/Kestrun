<#
.SYNOPSIS
    Convert a list (array) to a generic List[object], converting any nested PSObjects to generic objects.
.DESCRIPTION
    This function takes a list (array) as input and converts it to a System.Collections.Generic.List[object]. It ensures that any nested PSObjects are converted to generic objects.
    The order of elements in the list is preserved in the resulting generic list.
.PARAMETER Data
    The list (array) to convert.
.EXAMPLE
    $list = @( "Value1", [PSCustomObject]@{ Prop1 = "Val1"; Prop2 = "Val2" } )
    $genericList = Convert-ListToGenericList -Data $list
    # $genericList is now a List[object] with the same elements, where the second element is a generic object.
.NOTES
    This function is designed to work with PowerShell 7.0 and above.
#>
function Convert-ListToGenericList {
    param(
        [Parameter(Mandatory = $false)]
        [array]$Data = @()
    )
    $ret = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $Data.Count; $i++) {
        $ret.Add((Convert-PSObjectToGenericObject $Data[$i]))
    }
    # Return the generic list directly (do NOT wrap in a single-element array) so single-element
    # sequences remain proper YAML sequences and do not collapse into mappings during round-trip.
    return $ret
}
