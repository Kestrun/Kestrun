<#
    .SYNOPSIS
        Expands an object into a formatted string for display.
    .DESCRIPTION
        This function takes an object and formats it for display in the console. It includes the type name and the object's string representation.
        If a label is provided, it prefixes the output with the label.
    .PARAMETER InputObject
        The object to expand and display. This can be any PowerShell object, including complex types.
    .PARAMETER ForegroundColor
        The color to use for the output text in the console. If not specified, defaults to the console's current foreground color.
    .PARAMETER Label
        An optional label to prefix the output. This can be used to provide context or a name for the object being displayed.
    .EXAMPLE
        Expand-KrObject -InputObject $myObject -ForegroundColor Cyan -Label "My Object"
        Displays the $myObject with a cyan foreground color and prefixes it with "My Object".
    .NOTES
        This function is designed to be used in the context of Kestrun for debugging or logging purposes.
#>
function Expand-KrObject {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Console')]
    [OutputType([string])]
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [object] $InputObject,

        [Parameter(ParameterSetName = 'Console')]
        [System.ConsoleColor] $ForegroundColor,

        [Parameter()]
        [string] $Label,

        [Parameter(ParameterSetName = 'PassThru')]
        [switch]$PassThru
    )

    process {

        if ($null -eq $InputObject) {
            $InputObject = "`tNull Value"
        } else {
            $type = $InputObject.gettype().FullName
            $InputObject = $InputObject | Out-String
            $InputObject = "`tTypeName: $type`n$InputObject"
        }
        if ($Label) {
            $InputObject = "`tName: $Label $InputObject"
        }

        # Return the object if PassThru is specified
        if ($PassThru) {
            return $InputObject
        }

        if ($ForegroundColor) {
            if ($pipelineValue.Count -gt 1) {
                $InputObject | Write-Host -ForegroundColor $ForegroundColor
            } else {
                Write-Host -Object $InputObject -ForegroundColor $ForegroundColor
            }
        } else {
            if ($pipelineValue.Count -gt 1) {
                $InputObject | Write-Host
            } else {
                Write-Host -Object $InputObject
            }
        }
    }
}

