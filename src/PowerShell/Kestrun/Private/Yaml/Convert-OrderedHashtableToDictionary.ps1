# Portions derived from PowerShell-Yaml (https://github.com/cloudbase/powershell-yaml)
# Copyright (c) 2016–2024 Cloudbase Solutions Srl
# Licensed under the Apache License, Version 2.0 (Apache-2.0).
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Modifications Copyright (c) 2025 Kestrun Contributors

<#
.SYNOPSIS
    Convert an OrderedHashtable to an OrderedDictionary, converting any nested PSObjects to generic objects.
.DESCRIPTION
    This function takes an OrderedHashtable as input and converts it to an OrderedDictionary. It ensures that any nested PSObjects are converted to generic objects.
    The order of keys in the OrderedHashtable is preserved in the resulting OrderedDictionary.
.PARAMETER Data
    The OrderedHashtable to convert.
.EXAMPLE
    $oht = [ordered]@{ "Key1" = "Value1"; "Key2" = [PSCustomObject]@{ Prop1 = "Val1"; Prop2 = "Val2" } }
    $od = Convert-OrderedHashtableToDictionary $oht
    $od is now an OrderedDictionary with Key1 and Key2, where Key2's value is a generic object.
.NOTES
    This function is designed to work with PowerShell 7.0 and above.
#>
function Convert-OrderedHashtableToDictionary {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Specialized.OrderedDictionary] $Data
    )
    foreach ($i in $($data.PSBase.Keys)) {
        $Data[$i] = Convert-PSObjectToGenericObject $Data[$i]
    }
    return $Data
}
