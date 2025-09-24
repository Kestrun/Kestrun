# Portions derived from PowerShell-Yaml (https://github.com/cloudbase/powershell-yaml)
# Copyright (c) 2016–2024 Cloudbase Solutions Srl
# Licensed under the Apache License, Version 2.0 (Apache-2.0).
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Modifications Copyright (c) 2025 Kestrun Contributors

<#
.SYNOPSIS
    Converts a PowerShell object or hashtable to a YAML string.
.DESCRIPTION
    The ConvertTo-KrYaml cmdlet converts a PowerShell object or hashtable to a
    YAML string. This is useful for serializing data in a human-readable format.
.PARAMETER InputObject
    The PowerShell object or hashtable to convert. This parameter is mandatory and accepts input from the pipeline.
.PARAMETER Options
    Specifies serialization options for the YAML output. This parameter is available in the 'Options' parameter set.
.PARAMETER JsonCompatible
    If specified, the YAML output will be formatted to be compatible with JSON. This parameter is available in the 'NoOptions' parameter set.
.PARAMETER KeepArray
    If specified, the output will always be an array, even if there is only a single input object. By default, a single input object will result in a non-array output.

.NOTES
    This cmdlet requires PowerShell 7.0 or later.
    It uses the Kestrun.Utilities.Yaml library for YAML serialization.
.EXAMPLE
    $obj = [PSCustomObject]@{ Name = "John"; Age = 30; Skills = @("PowerShell", "YAML") }
    $yaml = $obj | ConvertTo-KrYaml
    # Outputs the YAML representation of the object to the console.
.EXAMPLE
    $obj = [PSCustomObject]@{ Name = "John"; Age = 30; Skills = @("PowerShell", "YAML") }
    $obj | ConvertTo-KrYaml -OutFile "output.yaml" -Force
    # Saves the YAML representation of the object to 'output.yaml', overwriting the file if it already exists.
.EXAMPLE
    $obj = [PSCustomObject]@{ Name = "John"; Age = 30; Skills = @("PowerShell", "YAML") }
    $yaml = $obj | ConvertTo-KrYaml -JsonCompatible
    # Outputs the YAML representation of the object in a JSON-compatible format to the console.
#>
function ConvertTo-KrYaml {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([string])]
    [CmdletBinding(DefaultParameterSetName = 'NoOptions')]
    param(
        [Parameter(ValueFromPipeline = $true, Position = 0)]
        [System.Object]$InputObject,

        [Parameter(ParameterSetName = 'Options')]
        [Kestrun.Utilities.Yaml.SerializationOptions]$Options = [Kestrun.Utilities.Yaml.SerializationOptions]::Roundtrip,

        [Parameter(ParameterSetName = 'NoOptions')]
        [switch]$JsonCompatible,

        [switch]$KeepArray
    )
    begin {
        $d = [System.Collections.Generic.List[object]]::new()
    }
    process {
        if ($null -ne $InputObject) {
            $d.Add($InputObject)
        }
    }
    end {
        if ($d -eq $null -or $d.Count -eq 0) {
            return
        }
        if ($d.Count -eq 1 -and !($KeepArray)) {
            $d = $d[0]
        }
        $norm = Convert-PSObjectToGenericObject -InputObject $d

        if ( $JsonCompatible.IsPresent) {
            # No indent options :~(
            $Options = [Kestrun.Utilities.Yaml.SerializationOptions]::JsonCompatible
        }

        $out = [Kestrun.Utilities.Yaml.YamlHelper]::ToYaml($norm, $Options)
        return $out
    }
}
