<#
    .SYNOPSIS
        Sets a 32-bit integer value in the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and sets the 32-bit integer value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to set.
    .PARAMETER Value
        The 32-bit integer value to set in the session.
    .EXAMPLE
        Set-KrSessionInt32 -Key "visitCount" -Value 5
        Sets the 32-bit integer value associated with the key "visitCount" in the session to 5.
    .OUTPUTS
        None. This function performs a state-changing operation on the session.
#>
function Set-KrSessionInt32 {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,
        [Parameter(Mandatory)]
        [int]$Value
    )
    if ($null -ne $Context.Session) {
        [Microsoft.AspNetCore.Http.SessionExtensions]::SetInt32($Context.Session, $Key, $Value)
    }
}
