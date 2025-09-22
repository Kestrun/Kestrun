<#
.SYNOPSIS
    Converts a PowerShell object or hashtable to a YAML string.
.DESCRIPTION
    The ConvertTo-KrYaml cmdlet converts a PowerShell object or hashtable to a
    YAML string. This is useful for serializing data in a human-readable format.
.PARAMETER InputObject
    The PowerShell object or hashtable to convert. This parameter is mandatory and accepts input from the pipeline.
.EXAMPLE
    $obj = [pscustomobject]@{
        name = 'John Doe'
        age  = 30
        city = 'New York'
    }
    $yaml = $obj | ConvertTo-KrYaml
    $yaml | Out-File -FilePath 'output.yaml'
    This example converts a PSCustomObject to a YAML string and saves it to a file.
.EXAMPLE
    $hash = @{
        name = 'John Doe'
        age  = 30
        city = 'New York'
    }
    $yaml = $hash | ConvertTo-KrYaml
    $yaml | Out-File -FilePath 'output.yaml'
    This example converts a hashtable to a YAML string and saves it to a file.
#>
function ConvertTo-KrYaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [outputtype([string])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [psobject]$InputObject
    )

    process {
        return [Kestrun.Utilities.YamlHelper]::ToYaml($InputObject)
    }
}
