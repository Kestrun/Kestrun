<#
.SYNOPSIS
    Converts a YAML string to a PowerShell object or hashtable.
.DESCRIPTION
    The ConvertFrom-KrYaml cmdlet converts a YAML string to a PowerShell object or
    hashtable. By default, it returns a PSCustomObject, but you can specify the
    -AsHashtable switch to get a hashtable instead.
.PARAMETER InputObject
    The YAML string to convert. This parameter is mandatory and accepts input from the pipeline.
.PARAMETER AsHashtable
    If specified, the cmdlet returns a hashtable instead of a PSCustomObject.
.EXAMPLE
    $yaml = @"
    name: John Doe
    age: 30
    city: New York
"@
    $result = $yaml | ConvertFrom-KrYaml
    $result | Format-List

    # Output:
    # name : John Doe
    # age  : 30
    # city : New York
    This example converts a YAML string to a PSCustomObject and displays its properties.
.EXAMPLE
    $yaml = @"
    name: John Doe
    age: 30
    city: New York
"@
    $result = $yaml | ConvertFrom-KrYaml -AsHashtable
    $result['name']  # Output: John Doe
    This example converts a YAML string to a hashtable and accesses a property by key.
#>
function ConvertFrom-KrYaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [outputtype([hashtable])]
    [outputtype([psobject])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string] $InputObject,
        [Parameter()]
        [switch]$AsHashtable
    )

    process {
        if ($AsHashtable) {
            return [Kestrun.Utilities.YamlHelper]::ToHashtable($InputObject )
        } else {
            return [Kestrun.Utilities.YamlHelper]::ToPSCustomObject($InputObject )
        }
    }
}
