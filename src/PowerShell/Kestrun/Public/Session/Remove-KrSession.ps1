<#
    .SYNOPSIS
        Removes a session item by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and removes the item
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to remove.
    .EXAMPLE
        Remove-KrSession -Key "username"
        Removes the session item associated with the key "username".
    .OUTPUTS
        None. This function performs a state-changing operation on the session.
#>
function Remove-KrSession {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key
    )
    if ($null -ne $Context.Session) {
        $Context.Session.Remove($Key)
    }
}
