<#
    .SYNOPSIS
        Retrieves a 32-bit integer value from the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and retrieves the 32-bit integer value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to retrieve.
    .EXAMPLE
        $value = Get-KrSessionInt32 -Key "visitCount"
        Retrieves the 32-bit integer value associated with the key "visitCount" from the session.
    .OUTPUTS
        Returns the 32-bit integer value associated with the specified key, or $null if not found.
#>
function Get-KrSessionInt32 {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([System.Nullable`1[[System.Int32]]])]
    param(
        [Parameter(Mandatory)][string]$Key
    )
    if ($null -ne $Context.Session) {
        return [Microsoft.AspNetCore.Http.SessionExtensions]::GetInt32($Context.Session, $Key)
    }
}
