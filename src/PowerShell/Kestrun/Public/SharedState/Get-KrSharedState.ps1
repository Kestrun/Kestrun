<#
    .SYNOPSIS
        Retrieves the value of a previously defined global variable.
    .DESCRIPTION
        Looks up a variable in the Kestrun global variable table and returns its
        value. If the variable does not exist, `$null` is returned.
    .PARAMETER Name
        Name of the variable to retrieve.
        This should be the fully qualified name of the variable, including any
        namespaces.
    .EXAMPLE
        Get-KrSharedState -Name "MyVariable"
        This retrieves the value of the global variable "MyVariable".
    .NOTES
        This function is part of the Kestrun.SharedState module and is used to retrieve the value of global variables.
#>
function Get-KrSharedState {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )
    process {
        # Retrieve (or $null if not defined)
        return [Kestrun.SharedState.SharedStateStore]::Get($Name)
    }
}
