<#
    .SYNOPSIS
        Defines or updates a global variable accessible across Kestrun scripts.

    .DESCRIPTION
        Stores a value in the Kestrun global variable table. Variables may be marked
        as read-only to prevent accidental modification.
        If the variable already exists, its value is updated. If it does not exist,
        it is created.
    .PARAMETER Name
        Name of the variable to create or update.
    .PARAMETER Value
        Value to assign to the variable.
    .PARAMETER AllowsValueType
        If specified, allows the variable to hold value types (e.g., int, bool).
    .EXAMPLE
        Set-KrSharedState -Name "MyVariable" -Value "Hello, World!"
        This creates a global variable "MyVariable" with the value "Hello, World!".
    .EXAMPLE
        Set-KrSharedState -Name "MyNamespace.MyVariable" -Value @{item=42}
        This creates a global variable "MyNamespace.MyVariable" with the value @{item=42}.
    .NOTES
        This function is part of the Kestrun.SharedState module and is used to define or update global variables.
#>
function Set-KrSharedState {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [object]$Value,

        [Parameter()]
        [switch]$AllowsValueType
    )
    process {
        # Define or update the variable; throws if it was already read-only
        $null = [Kestrun.SharedState.SharedStateStore]::Set(
            $Name,
            $Value,
            $AllowsValueType.IsPresent  # Allow value types if specified
        )
    }
}
