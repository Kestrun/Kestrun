<#
    .SYNOPSIS
        Sets a byte array value in the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and sets the byte array value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to set.
    .PARAMETER Value
        The byte array value to set in the session.
    .EXAMPLE
        Set-KrSessionByte -Key "profileImage" -Value $byteArray
        Sets the byte array value associated with the key "profileImage" in the session to $byteArray.
    .OUTPUTS
        None. This function performs a state-changing operation on the session.
#>
function Set-KrSessionByte {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,
        [Parameter(Mandatory)]
        [byte[]]$Value
    )
    if ($null -ne $Context.Session) {
        $Context.Session.Set($Key, $Value)
    }
}
