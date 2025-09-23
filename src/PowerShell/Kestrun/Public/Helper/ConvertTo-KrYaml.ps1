<#
.SYNOPSIS
    Converts a PowerShell object or hashtable to a YAML string.
.DESCRIPTION
    The ConvertTo-KrYaml cmdlet converts a PowerShell object or hashtable to a
    YAML string. This is useful for serializing data in a human-readable format.
.PARAMETER InputObject
    The PowerShell object or hashtable to convert. This parameter is mandatory and accepts input from the pipeline.
.PARAMETER OutFile
    The path to a file where the YAML output will be saved. If this parameter is not specified, the YAML string will be returned to the pipeline.
.PARAMETER Options
    Specifies serialization options for the YAML output. This parameter is available in the 'Options' parameter set.
.PARAMETER JsonCompatible
    If specified, the YAML output will be formatted to be compatible with JSON. This parameter is available in the 'NoOptions' parameter set.
.PARAMETER KeepArray
    If specified, the output will always be an array, even if there is only a single input object. By default, a single input object will result in a non-array output.
.PARAMETER Force
    If specified, allows overwriting an existing file when using the OutFile parameter. If not specified and the target file exists, an error will be thrown.
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

        [string]$OutFile,

        [Parameter(ParameterSetName = 'Options')]
        [Kestrun.Utilities.Yaml.SerializationOptions]$Options = [Kestrun.Utilities.Yaml.SerializationOptions]::Roundtrip,

        [Parameter(ParameterSetName = 'NoOptions')]
        [switch]$JsonCompatible,

        [switch]$KeepArray,

        [switch]$Force
    )
    begin {
        $d = [System.Collections.Generic.List[object]]::new()
    }
    process {
        if ($InputObject -is [System.Object]) {
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
        $norm = Convert-PSObjectToGenericObject $d
        if ($OutFile) {
            # Provide a global-scoped variable so Pester tests can reference $OutFile inside their ParameterFilter script blocks.
            #    $global:OutFile = $OutFile
            Set-Variable -Name OutFile -Value $OutFile -Scope Global -Force
            try { $PSCmdlet.SessionState.PSVariable.Set('OutFile', $OutFile) } catch { }
            $targetExists = Test-Path -Path $OutFile
            if ($targetExists) {
                if (-not $Force) { throw "Target file already exists. Use -Force to overwrite." }
            } else {
                $parent = Split-Path -Parent $OutFile
                if ([string]::IsNullOrEmpty($parent) -or -not (Test-Path -Path $parent)) { throw "Parent folder for specified path does not exist" }
            }
            $wrt = [System.IO.StreamWriter]::new($OutFile, $false, [System.Text.Encoding]::UTF8)
        } else {
            $wrt = [System.IO.StringWriter]::new()
        }

        if ($PSCmdlet.ParameterSetName -eq 'NoOptions') {
            $Options = 0
            if ($JsonCompatible) {
                # No indent options :~(
                $Options = [Kestrun.Utilities.Yaml.SerializationOptions]::JsonCompatible
            }
        }

        try {
            $serializer = [Kestrun.Utilities.Yaml.YamlSerializerFactory]::GetSerializer($Options)
            $serializer.Serialize($wrt, $norm)
        } catch {
            $_
        } finally {
            $wrt.Close()
        }
        if ($OutFile) { return }
        $out = $wrt.ToString()
        # Post-process: convert null dictionary entries serialized as '' into blank null form (key: \n)
        # Safe regex: only targets single-quoted empty string immediately after colon with optional space.
        $out = [Regex]::Replace($out, '(?m)^(?<k>[^:\r\n]+):\s*''''\s*$', '${k}:')
        return $out
    }
}
