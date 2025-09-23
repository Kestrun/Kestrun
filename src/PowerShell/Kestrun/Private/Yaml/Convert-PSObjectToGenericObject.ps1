# Portions derived from PowerShell-Yaml (https://github.com/cloudbase/powershell-yaml)
# Copyright (c) 2016–2024 Cloudbase Solutions Srl
# Licensed under the Apache License, Version 2.0 (Apache-2.0).
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Modifications Copyright (c) 2025 Kestrun Contributors

<#
.SYNOPSIS
    Convert a PSObject to a generic object, converting any nested hashtables or lists as well.
.DESCRIPTION
    This function takes a PSObject as input and converts it to a generic object. It ensures that any nested hashtables or lists are also converted to generic objects or generic lists, respectively.
.PARAMETER Data
    The PSObject to convert.
.EXAMPLE
    $psObj = [PSCustomObject]@{ Key1 = "Value1"; Key2 = @( "Val2a", "Val2b" ); Key3 = @{ SubKey = "SubVal" } }
    $genericObj = Convert-PSObjectToGenericObject -Data $psObj
    # $genericObj is now a generic object with the same properties, where Key2 is a List[object] and Key3 is an OrderedDictionary.
.NOTES
    This function is designed to work with PowerShell 7.0 and above.
#>
function Convert-PSObjectToGenericObject {
    param(
        [Parameter(Mandatory = $false)]
        [System.Object]$Data
    )

    if ($null -eq $data) {
        return $data
    }

    $dataType = $data.GetType()
    if (([System.Collections.Specialized.OrderedDictionary].IsAssignableFrom($dataType))) {
        return Convert-OrderedHashtableToDictionary $data
    } elseif (([System.Collections.IDictionary].IsAssignableFrom($dataType))) {
        return Convert-HashtableToDictionary $data
    } elseif (([System.Collections.IList].IsAssignableFrom($dataType))) {
        return Convert-ListToGenericList $data
    }
    return $data
}
