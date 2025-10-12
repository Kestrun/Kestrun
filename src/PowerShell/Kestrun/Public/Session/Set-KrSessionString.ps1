<#
    .SYNOPSIS
        Sets a string value in the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and sets the string value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to set.
    .PARAMETER Value
        The string value to set in the session.
    .EXAMPLE
        Set-KrSessionString -Key "userName" -Value "JohnDoe"
        Sets the string value associated with the key "userName" in the session to "JohnDoe".
    .OUTPUTS
        None. This function performs a state-changing operation on the session.
#>
function Set-KrSessionString {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,
        [Parameter(Mandatory)]
        [string]$Value
    )
    if ($null -ne $Context.Session) {
        [Microsoft.AspNetCore.Http.SessionExtensions]::SetString($Context.Session, $Key, $Value)
    }
}
